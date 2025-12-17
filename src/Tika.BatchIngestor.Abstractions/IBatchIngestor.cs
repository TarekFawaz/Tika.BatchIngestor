using System.Data.Common;

namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Core interface for batch data ingestion into relational databases.
/// </summary>
/// <typeparam name="T">The type of data to ingest.</typeparam>
public interface IBatchIngestor<T>
{
    /// <summary>
    /// Ingests data asynchronously from an async enumerable source.
    /// </summary>
    /// <param name="data">The data source to ingest.</param>
    /// <param name="tableName">The target database table name.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>Metrics about the ingestion operation.</returns>
    Task<BatchIngestMetrics> IngestAsync(
        IAsyncEnumerable<T> data,
        string tableName,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ingests data synchronously from an enumerable source.
    /// </summary>
    /// <param name="data">The data source to ingest.</param>
    /// <param name="tableName">The target database table name.</param>
    /// <param name="cancellationToken">Cancellation token to abort the operation.</param>
    /// <returns>Metrics about the ingestion operation.</returns>
    Task<BatchIngestMetrics> IngestAsync(
        IEnumerable<T> data,
        string tableName,
        CancellationToken cancellationToken = default);
}
