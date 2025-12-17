using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Dialects;

public class GenericSqlDialect : ISqlDialect
{
    public virtual string QuoteIdentifier(string identifier) => $"\"{identifier}\"";
    public virtual string GetParameterName(int index) => $"@p{index}";
    public virtual int GetMaxParametersPerCommand() => 32767;

    public virtual string BuildMultiRowInsert(
        string tableName,
        IReadOnlyList<string> columns,
        int rowCount)
    {
        if (string.IsNullOrWhiteSpace(tableName))
            throw new ArgumentException("Table name cannot be empty.", nameof(tableName));
        
        if (columns == null || columns.Count == 0)
            throw new ArgumentException("Columns list cannot be empty.", nameof(columns));
        
        if (rowCount <= 0)
            throw new ArgumentException("Row count must be greater than 0.", nameof(rowCount));

        var quotedTable = QuoteIdentifier(tableName);
        var quotedColumns = string.Join(", ", columns.Select(QuoteIdentifier));

        var valueRows = new List<string>(rowCount);
        var paramIndex = 0;

        for (int row = 0; row < rowCount; row++)
        {
            var paramNames = new List<string>(columns.Count);
            for (int col = 0; col < columns.Count; col++)
            {
                paramNames.Add(GetParameterName(paramIndex++));
            }
            valueRows.Add($"({string.Join(", ", paramNames)})");
        }

        return $"INSERT INTO {quotedTable} ({quotedColumns}) VALUES {string.Join(", ", valueRows)}";
    }
}
