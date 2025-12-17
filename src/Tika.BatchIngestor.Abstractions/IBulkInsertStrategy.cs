using System.Data.Common;

namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Strategy interface for executing bulk insert operations.
/// Implementations can use provider-specific bulk APIs or generic SQL.
/// </summary>
/// <typeparam name="T">The type of data being inserted.</typeparam>
public interface IBulkInsertStrategy<T>
{
    /// <summary>
    /// Executes a bulk insert operation for a batch of rows.
    /// </summary>
    /// <param name="connection">The database connection to use.</param>
    /// <param name="tableName">The target table name.</param>
    /// <param name="columns">The list of column names.</param>
    /// <param name="rows">The batch of rows to insert.</param>
    /// <param name="mapper">The mapper to convert rows to column values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The number of rows inserted.</returns>
    Task<int> ExecuteAsync(
        DbConnection connection,
        string tableName,
        IReadOnlyList<string> columns,
        IReadOnlyList<T> rows,
        IRowMapper<T> mapper,
        CancellationToken cancellationToken);
}
