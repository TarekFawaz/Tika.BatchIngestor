using System.Data.Common;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Factories;

public class SimpleConnectionFactory : IConnectionFactory
{
    private readonly string _connectionString;
    private readonly Func<DbConnection> _connectionFactory;

    public SimpleConnectionFactory(string connectionString, Func<DbConnection> connectionFactory)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("Connection string cannot be empty.", nameof(connectionString));
        
        _connectionString = connectionString;
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
    }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = _connectionFactory();
        connection.ConnectionString = _connectionString;
        await connection.OpenAsync(cancellationToken);
        return connection;
    }
}
