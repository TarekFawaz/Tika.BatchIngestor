namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Abstracts SQL dialect differences across database providers.
/// </summary>
public interface ISqlDialect
{
    /// <summary>
    /// Quotes an identifier (table or column name) for safe use in SQL.
    /// </summary>
    /// <param name="identifier">The identifier to quote.</param>
    /// <returns>The properly quoted identifier.</returns>
    string QuoteIdentifier(string identifier);

    /// <summary>
    /// Gets the parameter placeholder name for the given index.
    /// </summary>
    /// <param name="index">Zero-based parameter index.</param>
    /// <returns>The parameter name (e.g., "@p0", "?", "$1").</returns>
    string GetParameterName(int index);

    /// <summary>
    /// Gets the maximum number of parameters allowed per command for this dialect.
    /// </summary>
    /// <returns>Maximum parameter count, or int.MaxValue if unlimited.</returns>
    int GetMaxParametersPerCommand();

    /// <summary>
    /// Builds a multi-row INSERT statement for the specified table and columns.
    /// </summary>
    /// <param name="tableName">The target table name.</param>
    /// <param name="columns">The list of column names.</param>
    /// <param name="rowCount">The number of rows to insert in this statement.</param>
    /// <returns>A parameterized SQL INSERT statement.</returns>
    string BuildMultiRowInsert(
        string tableName,
        IReadOnlyList<string> columns,
        int rowCount);
}
