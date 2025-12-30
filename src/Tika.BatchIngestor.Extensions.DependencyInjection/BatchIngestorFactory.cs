using System.Collections.Concurrent;
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
    /// Creates a new BatchIngestor for SQL Server.
    /// </summary>
    /// <typeparam name="T">The entity type to ingest.</typeparam>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="mapper">Row mapper for the entity type.</param>
    /// <param name="options">Optional batch ingest options.</param>
    /// <returns>A configured batch ingestor.</returns>
    IBatchIngestor<T> CreateSqlServerIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null);

    /// <summary>
    /// Creates a new BatchIngestor for PostgreSQL.
    /// </summary>
    /// <typeparam name="T">The entity type to ingest.</typeparam>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="mapper">Row mapper for the entity type.</param>
    /// <param name="options">Optional batch ingest options.</param>
    /// <returns>A configured batch ingestor.</returns>
    IBatchIngestor<T> CreatePostgreSqlIngestor<T>(
        string connectionString,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null);

    /// <summary>
    /// Creates a new BatchIngestor with a custom connection factory and dialect.
    /// </summary>
    /// <typeparam name="T">The entity type to ingest.</typeparam>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="connectionFactory">Factory to create database connections.</param>
    /// <param name="dialect">SQL dialect for the target database.</param>
    /// <param name="mapper">Row mapper for the entity type.</param>
    /// <param name="options">Optional batch ingest options.</param>
    /// <returns>A configured batch ingestor.</returns>
    IBatchIngestor<T> CreateIngestor<T>(
        string connectionString,
        Func<DbConnection> connectionFactory,
        ISqlDialect dialect,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null);
}

/// <summary>
/// Default implementation of IBatchIngestorFactory.
/// </summary>
public class BatchIngestorFactory : IBatchIngestorFactory
{
    private readonly ILoggerFactory? _loggerFactory;

    public BatchIngestorFactory(ILoggerFactory? loggerFactory = null)
    {
        _loggerFactory = loggerFactory;
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

    public IBatchIngestor<T> CreateIngestor<T>(
        string connectionString,
        Func<DbConnection> connectionFactory,
        ISqlDialect dialect,
        IRowMapper<T> mapper,
        BatchIngestOptions? options = null)
    {
        var connFactory = new SimpleConnectionFactory(connectionString, connectionFactory);

        options ??= new BatchIngestOptions();
        options.Logger ??= _loggerFactory?.CreateLogger<BatchIngestor<T>>();

        return new BatchIngestor<T>(connFactory, dialect, mapper, options);
    }
}
