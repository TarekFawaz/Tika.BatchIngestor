using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Npgsql;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Dialects;
using Tika.BatchIngestor.Factories;

namespace Tika.BatchIngestor.Extensions.DependencyInjection;

/// <summary>
/// Factory for creating BatchIngestor instances dynamically.
/// Useful when you need to create ingestors at runtime with different configurations.
/// </summary>
public interface IBatchIngestorFactory
{
    /// <summary>
    /// Creates a new BatchIngestor using dialect type string.
    /// This is the recommended method for configuration-driven scenarios.
    /// </summary>
    /// <typeparam name="T">The entity type to ingest.</typeparam>
    /// <param name="dialectType">Dialect type: SqlServer, PostgreSql, AuroraPostgreSql, AuroraMySql, AzureSql, Generic</param>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="mapper">Row mapper for the entity type.</param>
    /// <param name="options">Optional batch ingest options.</param>
    /// <returns>A configured batch ingestor.</returns>
    IBatchIngestor<T> CreateIngestor<T>(
        string dialectType,
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null);

    /// <summary>
    /// Creates a new BatchIngestor for SQL Server.
    /// </summary>
    IBatchIngestor<T> CreateSqlServerIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null);

    /// <summary>
    /// Creates a new BatchIngestor for PostgreSQL.
    /// </summary>
    IBatchIngestor<T> CreatePostgreSqlIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null);

    /// <summary>
    /// Creates a new BatchIngestor for Amazon Aurora PostgreSQL.
    /// </summary>
    IBatchIngestor<T> CreateAuroraPostgreSqlIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null);

    /// <summary>
    /// Creates a new BatchIngestor for Amazon Aurora MySQL.
    /// </summary>
    IBatchIngestor<T> CreateAuroraMySqlIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null);

    /// <summary>
    /// Creates a new BatchIngestor for Azure SQL Database.
    /// </summary>
    IBatchIngestor<T> CreateAzureSqlIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null);

    /// <summary>
    /// Creates a new BatchIngestor with a custom connection factory and dialect.
    /// </summary>
    IBatchIngestor<T> CreateIngestor<T>(
        string connectionString,
        Func<DbConnection> connectionFactory,
        ISqlDialect dialect,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null);

    /// <summary>
    /// Gets the SQL dialect for a given dialect type string.
    /// </summary>
    ISqlDialect GetDialect(string dialectType);

    /// <summary>
    /// Gets a connection factory function for a given dialect type.
    /// </summary>
    Func<DbConnection> GetConnectionFactory(string dialectType);
}

/// <summary>
/// Default implementation of IBatchIngestorFactory.
/// Supports all built-in dialects: SqlServer, PostgreSql, AuroraPostgreSql, AuroraMySql, AzureSql, Generic
/// </summary>
public class BatchIngestorFactory : IBatchIngestorFactory
{
    private readonly ILoggerFactory? _loggerFactory;
    private readonly BatchIngestorSettings? _settings;

    public BatchIngestorFactory(ILoggerFactory? loggerFactory = null, BatchIngestorSettings? settings = null)
    {
        _loggerFactory = loggerFactory;
        _settings = settings;
    }

    public IBatchIngestor<T> CreateIngestor<T>(
        string dialectType,
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null)
    {
        var dialect = GetDialect(dialectType);
        var connectionFactory = GetConnectionFactory(dialectType);
        return CreateIngestor(connectionString, connectionFactory, dialect, mapper, options);
    }

    public IBatchIngestor<T> CreateSqlServerIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null)
    {
        return CreateIngestor(
            connectionString,
            () => new SqlConnection(),
            new SqlServerDialect(),
            mapper,
            options);
    }

    public IBatchIngestor<T> CreatePostgreSqlIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null)
    {
        return CreateIngestor(
            connectionString,
            () => new NpgsqlConnection(),
            new GenericSqlDialect(),
            mapper,
            options);
    }

    public IBatchIngestor<T> CreateAuroraPostgreSqlIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null)
    {
        return CreateIngestor(
            connectionString,
            () => new NpgsqlConnection(),
            new AuroraPostgreSqlDialect(),
            mapper,
            options);
    }

    public IBatchIngestor<T> CreateAuroraMySqlIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null)
    {
        // Note: Requires MySqlConnector NuGet package
        // Using Func to defer the connection creation
        return CreateIngestor(
            connectionString,
            CreateMySqlConnection,
            new AuroraMySqlDialect(),
            mapper,
            options);
    }

    public IBatchIngestor<T> CreateAzureSqlIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null)
    {
        return CreateIngestor(
            connectionString,
            () => new SqlConnection(),
            new AzureSqlDialect(),
            mapper,
            options);
    }

    public IBatchIngestor<T> CreateIngestor<T>(
        string connectionString,
        Func<DbConnection> connectionFactory,
        ISqlDialect dialect,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null)
    {
        var connFactory = new SimpleConnectionFactory(connectionString, connectionFactory);

        options ??= CreateDefaultOptions();
        options.Logger ??= _loggerFactory?.CreateLogger<BatchIngestor<T>>();

        return new BatchIngestor<T>(connFactory, dialect, mapper, options);
    }

    public ISqlDialect GetDialect(string dialectType)
    {
        return dialectType?.ToLowerInvariant() switch
        {
            "sqlserver" => new SqlServerDialect(),
            "postgresql" or "postgres" => new GenericSqlDialect(),
            "aurorapostgresql" or "aurorapostgres" => new AuroraPostgreSqlDialect(),
            "auroramysql" => new AuroraMySqlDialect(),
            "azuresql" or "azure" => new AzureSqlDialect(),
            "generic" or _ => new GenericSqlDialect()
        };
    }

    public Func<DbConnection> GetConnectionFactory(string dialectType)
    {
        return dialectType?.ToLowerInvariant() switch
        {
            "sqlserver" or "azuresql" or "azure" => () => new SqlConnection(),
            "postgresql" or "postgres" or "aurorapostgresql" or "aurorapostgres" => () => new NpgsqlConnection(),
            "auroramysql" => CreateMySqlConnection,
            "generic" or _ => () => new NpgsqlConnection() // Default to Npgsql for generic
        };
    }

    private BatchIngestOptions CreateDefaultOptions()
    {
        if (_settings == null)
            return new BatchIngestOptions();

        return new BatchIngestOptions
        {
            BatchSize = _settings.DefaultBatchSize,
            MaxDegreeOfParallelism = _settings.DefaultMaxDegreeOfParallelism,
            EnableCpuThrottling = _settings.EnableCpuThrottling,
            MaxCpuPercent = _settings.MaxCpuPercent,
            ThrottleDelayMs = _settings.ThrottleDelayMs,
            EnablePerformanceMetrics = _settings.EnablePerformanceMetrics,
            UseTransactions = true,
            TransactionPerBatch = true,
            RetryPolicy = new RetryPolicy
            {
                MaxRetries = _settings.RetryPolicy.MaxRetries,
                InitialDelayMs = _settings.RetryPolicy.InitialDelayMs,
                MaxDelayMs = _settings.RetryPolicy.MaxDelayMs,
                UseExponentialBackoff = _settings.RetryPolicy.UseExponentialBackoff,
                UseJitter = _settings.RetryPolicy.UseJitter
            }
        };
    }

    /// <summary>
    /// Creates a MySQL connection. Override this method if using a different MySQL connector.
    /// Default implementation throws - requires MySqlConnector package.
    /// </summary>
    protected virtual DbConnection CreateMySqlConnection()
    {
        // Try to create MySqlConnection via reflection to avoid hard dependency
        var mySqlType = Type.GetType("MySqlConnector.MySqlConnection, MySqlConnector");
        if (mySqlType != null)
        {
            return (DbConnection)Activator.CreateInstance(mySqlType)!;
        }

        throw new InvalidOperationException(
            "MySqlConnector package is required for Aurora MySQL. " +
            "Install the MySqlConnector NuGet package: dotnet add package MySqlConnector");
    }
}
