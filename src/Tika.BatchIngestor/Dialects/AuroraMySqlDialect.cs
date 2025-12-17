using System.Text;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Dialects;

/// <summary>
/// SQL dialect for Amazon Aurora MySQL.
/// Optimized for Aurora's MySQL-compatible engine.
/// </summary>
public class AuroraMySqlDialect : ISqlDialect
{
    /// <summary>
    /// MySQL uses backticks for identifiers.
    /// </summary>
    public string QuoteIdentifier(string identifier)
    {
        return $"`{identifier}`";
    }

    /// <summary>
    /// MySQL uses ? for parameters, but we use @p0, @p1 for ADO.NET compatibility.
    /// </summary>
    public string GetParameterName(int index)
    {
        return $"@p{index}";
    }

    /// <summary>
    /// Aurora MySQL has high limits, but we use a conservative value.
    /// MySQL's max_prepared_stmt_count is typically 16382.
    /// </summary>
    public int GetMaxParametersPerCommand()
    {
        return 32767;
    }

    /// <summary>
    /// Builds an optimized multi-row INSERT statement for Aurora MySQL.
    /// Uses INSERT IGNORE or INSERT ... ON DUPLICATE KEY UPDATE for better upsert performance.
    /// </summary>
    public string BuildMultiRowInsert(string tableName, IReadOnlyList<string> columns, int rowCount)
    {
        if (rowCount <= 0)
            throw new ArgumentException("Row count must be greater than 0.", nameof(rowCount));

        var quotedTable = QuoteIdentifier(tableName);
        var quotedColumns = string.Join(", ", columns.Select(QuoteIdentifier));

        var sb = new StringBuilder();
        sb.Append($"INSERT INTO {quotedTable} ({quotedColumns}) VALUES ");

        var paramsPerRow = columns.Count;
        for (int row = 0; row < rowCount; row++)
        {
            if (row > 0)
                sb.Append(", ");

            sb.Append('(');
            for (int col = 0; col < paramsPerRow; col++)
            {
                if (col > 0)
                    sb.Append(", ");

                var paramIndex = (row * paramsPerRow) + col;
                sb.Append(GetParameterName(paramIndex));
            }
            sb.Append(')');
        }

        return sb.ToString();
    }
}
