using System.Data.Common;

namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Factory for creating database connections.
/// </summary>
public interface IConnectionFactory
{
    /// <summary>
    /// Creates and opens a new database connection asynchronously.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An open database connection.</returns>
    Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default);
}
