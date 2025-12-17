using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Abstractions.Exceptions;
using Tika.BatchIngestor.Internal;

namespace Tika.BatchIngestor;

/// <summary>
/// Main implementation of batch data ingestion.
/// </summary>
/// <typeparam name="T">The type of data to ingest.</typeparam>
public class BatchIngestor<T> : IBatchIngestor<T>
{
    private readonly IConnectionFactory _connectionFactory;
    private readonly ISqlDialect _dialect;
    private readonly IRowMapper<T> _mapper;
    private readonly BatchIngestOptions _options;
    private readonly IBulkInsertStrategy<T> _strategy;
    private readonly ILogger? _logger;

    /// <summary>
    /// Initializes a new instance of BatchIngestor.
    /// </summary>
    public BatchIngestor(
        IConnectionFactory connectionFactory,
        ISqlDialect dialect,
        IRowMapper<T> mapper,
        BatchIngestOptions options,
        IBulkInsertStrategy<T>? strategy = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _options.Validate();
        
        _strategy = strategy ?? new Strategies.GenericInsertStrategy<T>(_dialect, _options);
        _logger = _options.Logger;
    }

    /// <inheritdoc/>
    public async Task<BatchIngestMetrics> IngestAsync(
        IAsyncEnumerable<T> data,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        if (string.IsNullOrWhiteSpace(tableName)) throw new ArgumentException("Table name cannot be empty.", nameof(tableName));

        _logger?.LogInformation("Starting batch ingestion to table {TableName}", tableName);

        var stopwatch = Stopwatch.StartNew();
        var metrics = new BatchIngestMetrics();
        var processor = new BatchProcessor<T>(
            _connectionFactory,
            _strategy,
            _mapper,
            _options,
            tableName,
            metrics);

        try
        {
            await processor.ProcessAsync(data, cancellationToken);
            
            metrics.ElapsedTime = stopwatch.Elapsed;
            
            _logger?.LogInformation(
                "Batch ingestion completed. Rows: {Rows:N0}, Batches: {Batches}, Duration: {Duration}, Throughput: {Throughput:N0} rows/sec",
                metrics.TotalRowsProcessed,
                metrics.BatchesCompleted,
                metrics.ElapsedTime,
                metrics.RowsPerSecond);

            return metrics;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Batch ingestion failed");
            throw;
        }
    }

    /// <inheritdoc/>
    public Task<BatchIngestMetrics> IngestAsync(
        IEnumerable<T> data,
        string tableName,
        CancellationToken cancellationToken = default)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));
        
        return IngestAsync(data.ToAsyncEnumerable(), tableName, cancellationToken);
    }
}

internal static class EnumerableExtensions
{
    public static async IAsyncEnumerable<T> ToAsyncEnumerable<T>(this IEnumerable<T> source)
    {
        const int batchSize = 1000; // Yield control every N items instead of every item
        int count = 0;

        foreach (var item in source)
        {
            yield return item;

            // Only yield control periodically to avoid excessive context switching
            if (++count % batchSize == 0)
            {
                await Task.Yield();
            }
        }
    }
}
