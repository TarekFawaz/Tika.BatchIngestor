using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor;

/// <summary>
/// Default implementation of IRowMapper using a delegate function.
/// </summary>
/// <typeparam name="T">The type to map.</typeparam>
public class DefaultRowMapper<T> : IRowMapper<T>
{
    private readonly Func<T, IReadOnlyDictionary<string, object?>> _mapFunc;
    private IReadOnlyList<string>? _cachedColumns;

    /// <summary>
    /// Initializes a new instance of DefaultRowMapper.
    /// </summary>
    /// <param name="mapFunc">Function to map an item to column values.</param>
    public DefaultRowMapper(Func<T, IReadOnlyDictionary<string, object?>> mapFunc)
    {
        _mapFunc = mapFunc ?? throw new ArgumentNullException(nameof(mapFunc));
    }

    /// <inheritdoc/>
    public IReadOnlyDictionary<string, object?> Map(T item)
    {
        var result = _mapFunc(item);
        
        // Cache columns from first mapping
        _cachedColumns ??= result.Keys.ToList();
        
        return result;
    }

    /// <inheritdoc/>
    public IReadOnlyList<string> GetColumns()
    {
        return _cachedColumns ?? Array.Empty<string>();
    }
}
