using System.Text;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Dialects;

/// <summary>
/// SQL dialect for Amazon Aurora PostgreSQL.
/// Optimized for Aurora's PostgreSQL-compatible engine.
/// </summary>
public class AuroraPostgreSqlDialect : ISqlDialect
{
    /// <summary>
    /// PostgreSQL uses double quotes for identifiers.
    /// </summary>
    public string QuoteIdentifier(string identifier)
    {
        return $"\"{identifier}\"";
    }

    /// <summary>
    /// PostgreSQL uses $1, $2, $3 style parameters, but we use @p0, @p1 for compatibility.
    /// </summary>
    public string GetParameterName(int index)
    {
        return $"@p{index}";
    }

    /// <summary>
    /// Aurora PostgreSQL supports very high parameter counts.
    /// Conservative limit for stability.
    /// </summary>
    public int GetMaxParametersPerCommand()
    {
        return 32767; // PostgreSQL theoretical limit, but Aurora handles this well
    }

    /// <summary>
    /// Builds an optimized multi-row INSERT statement for Aurora PostgreSQL.
    /// Uses ON CONFLICT DO NOTHING for better upsert performance if needed.
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
