namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Maps a POCO to a dictionary of column names and values for database insertion.
/// </summary>
/// <typeparam name="T">The type to map.</typeparam>
public interface IRowMapper<T>
{
    /// <summary>
    /// Maps an item to a dictionary of column names to values.
    /// </summary>
    /// <param name="item">The item to map.</param>
    /// <returns>A dictionary where keys are column names and values are the corresponding data.</returns>
    IReadOnlyDictionary<string, object?> Map(T item);

    /// <summary>
    /// Gets the list of column names in the order they should appear in INSERT statements.
    /// </summary>
    /// <returns>An ordered list of column names.</returns>
    IReadOnlyList<string> GetColumns();
}
