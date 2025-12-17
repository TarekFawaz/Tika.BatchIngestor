using System.Data;
using System.Data.Common;
using Microsoft.Extensions.Logging;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Internal;

namespace Tika.BatchIngestor.Strategies;

public class GenericInsertStrategy<T> : IBulkInsertStrategy<T>
{
    private readonly ISqlDialect _dialect;
    private readonly BatchIngestOptions _options;
    private readonly ILogger? _logger;

    public GenericInsertStrategy(ISqlDialect dialect, BatchIngestOptions options)
    {
        _dialect = dialect ?? throw new ArgumentNullException(nameof(dialect));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = options.Logger;
    }

    public async Task<int> ExecuteAsync(
        DbConnection connection,
        string tableName,
        IReadOnlyList<string> columns,
        IReadOnlyList<T> rows,
        IRowMapper<T> mapper,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return 0;

        var maxParamsPerCommand = _dialect.GetMaxParametersPerCommand();
        var paramsPerRow = columns.Count;
        var maxRowsPerCommand = Math.Min(rows.Count, maxParamsPerCommand / paramsPerRow);

        if (maxRowsPerCommand <= 0)
        {
            throw new InvalidOperationException(
                $"Cannot insert rows: column count ({paramsPerRow}) exceeds maximum parameters per command ({maxParamsPerCommand}).");
        }

        var totalInserted = 0;
        var commandBuilder = new SqlCommandBuilder(_dialect, _options);

        for (int i = 0; i < rows.Count; i += maxRowsPerCommand)
        {
            var chunkSize = Math.Min(maxRowsPerCommand, rows.Count - i);
            var chunk = rows.Skip(i).Take(chunkSize).ToList();

            using var command = commandBuilder.BuildInsertCommand(
                connection,
                tableName,
                columns,
                chunk,
                mapper);

            var inserted = await command.ExecuteNonQueryAsync(cancellationToken);
            totalInserted += inserted;
        }

        return totalInserted;
    }
}
