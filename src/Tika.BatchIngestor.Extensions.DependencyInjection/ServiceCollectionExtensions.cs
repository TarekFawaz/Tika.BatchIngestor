using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Dialects;
using Tika.BatchIngestor.Factories;

namespace Tika.BatchIngestor.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering BatchIngestor services with the DI container.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds BatchIngestor services for SQL Server.
    /// </summary>
    /// <typeparam name="T">The entity type to ingest.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="mapperFactory">Factory function to create the row mapper.</param>
    /// <param name="configureOptions">Optional action to configure batch ingest options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddSqlServerBatchIngestor<T>(
        this IServiceCollection services,
        string connectionString,
        Func<IServiceProvider, IRowMapper<T>> mapperFactory,
        Action<BatchIngestOptions>? configureOptions = null)
    {
        return services.AddBatchIngestor<T>(
            connectionString,
            () => new SqlConnection(),
            new SqlServerDialect(),
            mapperFactory,
            configureOptions);
    }

    /// <summary>
    /// Adds BatchIngestor services for PostgreSQL.
    /// </summary>
    /// <typeparam name="T">The entity type to ingest.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="mapperFactory">Factory function to create the row mapper.</param>
    /// <param name="configureOptions">Optional action to configure batch ingest options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddPostgreSqlBatchIngestor<T>(
        this IServiceCollection services,
        string connectionString,
        Func<IServiceProvider, IRowMapper<T>> mapperFactory,
        Action<BatchIngestOptions>? configureOptions = null)
    {
        return services.AddBatchIngestor<T>(
            connectionString,
            () => new NpgsqlConnection(),
            new GenericSqlDialect(),
            mapperFactory,
            configureOptions);
    }

    /// <summary>
    /// Adds a generic BatchIngestor with custom connection and dialect.
    /// </summary>
    /// <typeparam name="T">The entity type to ingest.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="connectionString">Database connection string.</param>
    /// <param name="connectionFactory">Factory to create new database connections.</param>
    /// <param name="dialect">SQL dialect for the target database.</param>
    /// <param name="mapperFactory">Factory function to create the row mapper.</param>
    /// <param name="configureOptions">Optional action to configure batch ingest options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBatchIngestor<T>(
        this IServiceCollection services,
        string connectionString,
        Func<DbConnection> connectionFactory,
        ISqlDialect dialect,
        Func<IServiceProvider, IRowMapper<T>> mapperFactory,
        Action<BatchIngestOptions>? configureOptions = null)
    {
        services.TryAddSingleton<IConnectionFactory>(sp =>
            new SimpleConnectionFactory(connectionString, connectionFactory));

        services.TryAddSingleton(dialect);

        services.TryAddSingleton(mapperFactory);

        services.AddSingleton<IBatchIngestor<T>>(sp =>
        {
            var connFactory = sp.GetRequiredService<IConnectionFactory>();
            var sqlDialect = sp.GetRequiredService<ISqlDialect>();
            var mapper = mapperFactory(sp);
            var logger = sp.GetService<ILogger<BatchIngestor<T>>>();

            var options = new BatchIngestOptions { Logger = logger };
            configureOptions?.Invoke(options);

            return new BatchIngestor<T>(connFactory, sqlDialect, mapper, options);
        });

        return services;
    }

    /// <summary>
    /// Adds a keyed/named BatchIngestor for SQL Server.
    /// Useful when you need multiple ingestors for different tables or databases.
    /// </summary>
    /// <typeparam name="T">The entity type to ingest.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">Unique name for this ingestor.</param>
    /// <param name="connectionString">SQL Server connection string.</param>
    /// <param name="mapperFactory">Factory function to create the row mapper.</param>
    /// <param name="configureOptions">Optional action to configure batch ingest options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKeyedSqlServerBatchIngestor<T>(
        this IServiceCollection services,
        string name,
        string connectionString,
        Func<IServiceProvider, IRowMapper<T>> mapperFactory,
        Action<BatchIngestOptions>? configureOptions = null)
    {
        services.AddKeyedSingleton<IBatchIngestor<T>>(name, (sp, key) =>
        {
            var connFactory = new SimpleConnectionFactory(connectionString, () => new SqlConnection());
            var dialect = new SqlServerDialect();
            var mapper = mapperFactory(sp);
            var logger = sp.GetService<ILogger<BatchIngestor<T>>>();

            var options = new BatchIngestOptions { Logger = logger };
            configureOptions?.Invoke(options);

            return new BatchIngestor<T>(connFactory, dialect, mapper, options);
        });

        return services;
    }

    /// <summary>
    /// Adds a keyed/named BatchIngestor for PostgreSQL.
    /// Useful when you need multiple ingestors for different tables or databases.
    /// </summary>
    /// <typeparam name="T">The entity type to ingest.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="name">Unique name for this ingestor.</param>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="mapperFactory">Factory function to create the row mapper.</param>
    /// <param name="configureOptions">Optional action to configure batch ingest options.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddKeyedPostgreSqlBatchIngestor<T>(
        this IServiceCollection services,
        string name,
        string connectionString,
        Func<IServiceProvider, IRowMapper<T>> mapperFactory,
        Action<BatchIngestOptions>? configureOptions = null)
    {
        services.AddKeyedSingleton<IBatchIngestor<T>>(name, (sp, key) =>
        {
            var connFactory = new SimpleConnectionFactory(connectionString, () => new NpgsqlConnection());
            var dialect = new GenericSqlDialect();
            var mapper = mapperFactory(sp);
            var logger = sp.GetService<ILogger<BatchIngestor<T>>>();

            var options = new BatchIngestOptions { Logger = logger };
            configureOptions?.Invoke(options);

            return new BatchIngestor<T>(connFactory, dialect, mapper, options);
        });

        return services;
    }
}
