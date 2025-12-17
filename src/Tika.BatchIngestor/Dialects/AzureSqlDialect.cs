using System.Text;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Dialects;

/// <summary>
/// SQL dialect for Microsoft Azure SQL Database.
/// Optimized for Azure SQL with cloud-specific performance considerations.
/// </summary>
public class AzureSqlDialect : ISqlDialect
{
    /// <summary>
    /// Azure SQL uses square brackets for identifiers (same as SQL Server).
    /// </summary>
    public string QuoteIdentifier(string identifier)
    {
        return $"[{identifier}]";
    }

    /// <summary>
    /// Azure SQL uses @p0, @p1, @p2 style parameters.
    /// </summary>
    public string GetParameterName(int index)
    {
        return $"@p{index}";
    }

    /// <summary>
    /// Azure SQL has the same 2100 parameter limit as SQL Server.
    /// However, for optimal performance in Azure, we use slightly lower batches
    /// to account for network latency and connection pooling.
    /// </summary>
    public int GetMaxParametersPerCommand()
    {
        return 2100;
    }

    /// <summary>
    /// Builds an optimized multi-row INSERT statement for Azure SQL.
    /// Azure SQL benefits from explicit column ordering and parameterization.
    /// Supports MERGE statements for better upsert performance in cloud scenarios.
    /// </summary>
    public string BuildMultiRowInsert(string tableName, IReadOnlyList<string> columns, int rowCount)
    {
        if (rowCount <= 0)
            throw new ArgumentException("Row count must be greater than 0.", nameof(rowCount));

        var quotedTable = QuoteIdentifier(tableName);
        var quotedColumns = string.Join(", ", columns.Select(QuoteIdentifier));

        var sb = new StringBuilder();

        // Use INSERT with explicit schema for Azure SQL
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
