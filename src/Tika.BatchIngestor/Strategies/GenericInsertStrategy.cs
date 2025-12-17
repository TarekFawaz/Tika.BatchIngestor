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

        // Process in chunks without creating intermediate collections
        for (int i = 0; i < rows.Count; i += maxRowsPerCommand)
        {
            var chunkSize = Math.Min(maxRowsPerCommand, rows.Count - i);

            // Create a view of the chunk without allocating a new list
            var chunk = new ListSegment<T>(rows, i, chunkSize);

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

    /// <summary>
    /// Zero-allocation list segment wrapper.
    /// </summary>
    private readonly struct ListSegment<TItem> : IReadOnlyList<TItem>
    {
        private readonly IReadOnlyList<TItem> _list;
        private readonly int _offset;
        private readonly int _count;

        public ListSegment(IReadOnlyList<TItem> list, int offset, int count)
        {
            _list = list;
            _offset = offset;
            _count = count;
        }

        public TItem this[int index]
        {
            get
            {
                if (index < 0 || index >= _count)
                    throw new ArgumentOutOfRangeException(nameof(index));
                return _list[_offset + index];
            }
        }

        public int Count => _count;

        public IEnumerator<TItem> GetEnumerator()
        {
            for (int i = 0; i < _count; i++)
                yield return _list[_offset + i];
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
            => GetEnumerator();
    }
}
