using System.Data.Common;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Strategies;

public abstract class BulkInsertStrategyAdapter<T> : IBulkInsertStrategy<T>
{
    public abstract Task<int> ExecuteAsync(
        DbConnection connection,
        string tableName,
        IReadOnlyList<string> columns,
        IReadOnlyList<T> rows,
        IRowMapper<T> mapper,
        CancellationToken cancellationToken);

    public abstract bool IsCompatible(DbConnection connection);
}
