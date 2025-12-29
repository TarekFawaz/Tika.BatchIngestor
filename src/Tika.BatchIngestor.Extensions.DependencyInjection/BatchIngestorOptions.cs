namespace Tika.BatchIngestor.Extensions.DependencyInjection;

/// <summary>
/// Configuration options for database connections used by BatchIngestor.
/// </summary>
public class DatabaseConnectionOptions
{
    /// <summary>
    /// SQL Server connection string.
    /// </summary>
    public string? SqlServerConnectionString { get; set; }

    /// <summary>
    /// PostgreSQL connection string.
    /// </summary>
    public string? PostgreSqlConnectionString { get; set; }
}

/// <summary>
/// Named database connection configuration.
/// </summary>
public class NamedDatabaseConnection
{
    /// <summary>
    /// Unique name for this database connection.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Database type (SqlServer or PostgreSql).
    /// </summary>
    public DatabaseType DatabaseType { get; set; }

    /// <summary>
    /// Connection string for the database.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;
}

/// <summary>
/// Supported database types.
/// </summary>
public enum DatabaseType
{
    SqlServer,
    PostgreSql
}
