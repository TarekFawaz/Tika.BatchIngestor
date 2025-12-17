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

    private int _batchCounter;
    private long _totalBatchDurationMs;

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
            var batch = new List<T>(_options.BatchSize);

            await foreach (var item in data.WithCancellation(cancellationToken))
            {
                batch.Add(item);

                if (batch.Count >= _options.BatchSize)
                {
                    await writer.WriteAsync(batch, cancellationToken);
                    batch = new List<T>(_options.BatchSize);
                }
            }

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

            lock (_metrics)
            {
                _metrics.TotalRowsProcessed += batch.Count;
                _metrics.BatchesCompleted++;
                
                _totalBatchDurationMs += (long)duration.TotalMilliseconds;
                _metrics.AverageBatchDuration = TimeSpan.FromMilliseconds(
                    _totalBatchDurationMs / (double)_metrics.BatchesCompleted);

                if (duration < _metrics.MinBatchDuration)
                    _metrics.MinBatchDuration = duration;

                if (duration > _metrics.MaxBatchDuration)
                    _metrics.MaxBatchDuration = duration;
            }

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
            
            lock (_metrics)
            {
                _metrics.ErrorCount++;
            }

            throw new BatchIngestException(
                $"Failed to process batch {batchNumber}",
                batchNumber,
                _metrics.TotalRowsProcessed,
                ex);
        }
    }
}
