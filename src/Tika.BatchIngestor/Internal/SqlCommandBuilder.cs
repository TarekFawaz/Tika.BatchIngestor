using System.Data;
using System.Data.Common;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Internal;

internal class SqlCommandBuilder
{
    private readonly ISqlDialect _dialect;
    private readonly BatchIngestOptions _options;

    public SqlCommandBuilder(ISqlDialect dialect, BatchIngestOptions options)
    {
        _dialect = dialect;
        _options = options;
    }

    public DbCommand BuildInsertCommand<T>(
        DbConnection connection,
        string tableName,
        IReadOnlyList<string> columns,
        IReadOnlyList<T> rows,
        IRowMapper<T> mapper)
    {
        var sql = _dialect.BuildMultiRowInsert(tableName, columns, rows.Count);
        
        var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandTimeout = _options.CommandTimeoutSeconds;
        command.CommandType = CommandType.Text;

        var paramIndex = 0;
        foreach (var row in rows)
        {
            var mappedRow = mapper.Map(row);
            
            foreach (var column in columns)
            {
                var param = command.CreateParameter();
                param.ParameterName = _dialect.GetParameterName(paramIndex++);
                param.Value = mappedRow.TryGetValue(column, out var value) && value != null
                    ? value
                    : DBNull.Value;
                
                command.Parameters.Add(param);
            }
        }

        return command;
    }
}
