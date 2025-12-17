using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Abstractions.Exceptions;

namespace Tika.BatchIngestor.Internal;

internal class BatchProcessor<T>
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly IBulkInsertStrategy<T> _strategy;
    private readonly IRowMapper<T> _mapper;
    private readonly BatchIngestOptions _options;
    private readonly string _tableName;
    private readonly BatchIngestMetrics _metrics;
    private readonly ILogger? _logger;
    private readonly RetryExecutor _retryExecutor;
    private readonly PerformanceMetrics? _performanceMetrics;
    private readonly Timer? _metricsTimer;

    private int _batchCounter;

    public BatchProcessor(
        IConnectionFactory connectionFactory,
        IBulkInsertStrategy<T> strategy,
        IRowMapper<T> mapper,
        BatchIngestOptions options,
        string tableName,
        BatchIngestMetrics metrics)
    {
        _connectionFactory = connectionFactory;
        _strategy = strategy;
        _mapper = mapper;
        _options = options;
        _tableName = tableName;
        _metrics = metrics;
        _logger = options.Logger;
        _retryExecutor = new RetryExecutor(options.RetryPolicy, _logger);

        if (_options.EnablePerformanceMetrics)
        {
            _performanceMetrics = new PerformanceMetrics();
            _metricsTimer = new Timer(
                _ => CollectPerformanceMetrics(),
                null,
                TimeSpan.FromMilliseconds(_options.PerformanceMetricsIntervalMs),
                TimeSpan.FromMilliseconds(_options.PerformanceMetricsIntervalMs));
        }
    }

    private void CollectPerformanceMetrics()
    {
        if (_performanceMetrics == null) return;

        var snapshot = _performanceMetrics.CreateSnapshot();
        _metrics.UpdatePeakPerformance(snapshot);

        if (_options.EnableCpuThrottling && _options.MaxCpuPercent > 0)
        {
            if (snapshot.CpuUsagePercent > _options.MaxCpuPercent)
            {
                _logger?.LogWarning(
                    "CPU usage ({Cpu:F2}%) exceeds threshold ({Threshold:F2}%). Throttling enabled.",
                    snapshot.CpuUsagePercent,
                    _options.MaxCpuPercent);
            }
        }
    }

    public async Task ProcessAsync(IAsyncEnumerable<T> data, CancellationToken cancellationToken)
    {
        var channel = Channel.CreateBounded<List<T>>(new BoundedChannelOptions(_options.MaxInFlightBatches)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = true
        });

        var producerTask = Task.Run(() => ProduceAsync(data, channel.Writer, cancellationToken), cancellationToken);

        var consumerTasks = new List<Task>();
        for (int i = 0; i < _options.MaxDegreeOfParallelism; i++)
        {
            var consumerTask = Task.Run(() => ConsumeAsync(channel.Reader, cancellationToken), cancellationToken);
            consumerTasks.Add(consumerTask);
        }

        await producerTask;
        await Task.WhenAll(consumerTasks);
    }

    private async Task ProduceAsync(
        IAsyncEnumerable<T> data,
        ChannelWriter<List<T>> writer,
        CancellationToken cancellationToken)
    {
        try
        {
            // Reuse list instance with capacity pre-allocated
            var batch = new List<T>(_options.BatchSize);

            await foreach (var item in data.WithCancellation(cancellationToken))
            {
                batch.Add(item);

                if (batch.Count >= _options.BatchSize)
                {
                    // Send the batch to consumers
                    await writer.WriteAsync(batch, cancellationToken);

                    // Create new batch with same capacity for better memory allocation
                    batch = new List<T>(_options.BatchSize);
                }
            }

            // Send remaining items
            if (batch.Count > 0)
            {
                await writer.WriteAsync(batch, cancellationToken);
            }
        }
        finally
        {
            writer.Complete();
        }
    }

    private async Task ConsumeAsync(
        ChannelReader<List<T>> reader,
        CancellationToken cancellationToken)
    {
        await foreach (var batch in reader.ReadAllAsync(cancellationToken))
        {
            await ProcessBatchAsync(batch, cancellationToken);
        }
    }

    private async Task ProcessBatchAsync(List<T> batch, CancellationToken cancellationToken)
    {
        var batchNumber = Interlocked.Increment(ref _batchCounter);

        // CPU throttling check
        if (_options.EnableCpuThrottling && _options.MaxCpuPercent > 0 && _performanceMetrics != null)
        {
            var cpuUsage = _performanceMetrics.CpuUsagePercent;
            if (cpuUsage > _options.MaxCpuPercent)
            {
                _logger?.LogDebug(
                    "Throttling batch {BatchNumber} - CPU: {Cpu:F2}% > {Threshold:F2}%",
                    batchNumber,
                    cpuUsage,
                    _options.MaxCpuPercent);

                await Task.Delay(_options.ThrottleDelayMs, cancellationToken);
            }
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            await using var connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);

            var firstMapped = _mapper.Map(batch[0]);
            var columns = firstMapped.Keys.ToList();

            await _retryExecutor.ExecuteAsync(async () =>
            {
                if (_options.UseTransactions)
                {
                    if (_options.TransactionPerBatch)
                    {
                        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
                        try
                        {
                            await _strategy.ExecuteAsync(
                                connection,
                                _tableName,
                                columns,
                                batch,
                                _mapper,
                                cancellationToken);

                            await transaction.CommitAsync(cancellationToken);
                        }
                        catch
                        {
                            await transaction.RollbackAsync(cancellationToken);
                            throw;
                        }
                    }
                    else
                    {
                        await _strategy.ExecuteAsync(
                            connection,
                            _tableName,
                            columns,
                            batch,
                            _mapper,
                            cancellationToken);
                    }
                }
                else
                {
                    await _strategy.ExecuteAsync(
                        connection,
                        _tableName,
                        columns,
                        batch,
                        _mapper,
                        cancellationToken);
                }
            }, cancellationToken);

            stopwatch.Stop();
            var duration = stopwatch.Elapsed;

            // Use lock-free atomic operations
            _metrics.AddRowsProcessed(batch.Count);
            _metrics.IncrementBatchesCompleted();
            _metrics.RecordBatchDuration(duration);

            _options.OnBatchCompleted?.Invoke(batchNumber, duration);

            if (batchNumber % 10 == 0)
            {
                _options.OnProgress?.Invoke(_metrics.Clone());
            }

            _logger?.LogDebug(
                "Batch {BatchNumber} completed: {RowCount} rows in {Duration:N2}ms",
                batchNumber,
                batch.Count,
                duration.TotalMilliseconds);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to process batch {BatchNumber}", batchNumber);

            _metrics.IncrementErrorCount();

            throw new BatchIngestException(
                $"Failed to process batch {batchNumber}",
                batchNumber,
                _metrics.TotalRowsProcessed,
                ex);
        }
    }
}
