using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Tika.BatchIngestor.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering the BatchIngestorFactory.
/// </summary>
public static class FactoryServiceCollectionExtensions
{
    /// <summary>
    /// Adds the BatchIngestorFactory to the service collection.
    /// Use this when you need to create ingestors dynamically at runtime.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBatchIngestorFactory(this IServiceCollection services)
    {
        services.TryAddSingleton<IBatchIngestorFactory>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();
            var settings = sp.GetService<BatchIngestorSettings>();
            return new BatchIngestorFactory(loggerFactory, settings);
        });

        return services;
    }

    /// <summary>
    /// Adds the BatchIngestorFactory with settings from configuration.
    /// Reads from the "BatchIngestor" section in appsettings.json.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration root.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBatchIngestorFactory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var settings = configuration
            .GetSection(BatchIngestorSettings.SectionName)
            .Get<BatchIngestorSettings>() ?? new BatchIngestorSettings();

        services.AddSingleton(settings);

        services.TryAddSingleton<IBatchIngestorFactory>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();
            return new BatchIngestorFactory(loggerFactory, settings);
        });

        return services;
    }

    /// <summary>
    /// Adds the BatchIngestorFactory with custom settings.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="settings">The batch ingestor settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBatchIngestorFactory(
        this IServiceCollection services,
        BatchIngestorSettings settings)
    {
        services.AddSingleton(settings);

        services.TryAddSingleton<IBatchIngestorFactory>(sp =>
        {
            var loggerFactory = sp.GetService<ILoggerFactory>();
            return new BatchIngestorFactory(loggerFactory, settings);
        });

        return services;
    }

    /// <summary>
    /// Adds the BatchIngestorFactory with settings configured via action.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Action to configure settings.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddBatchIngestorFactory(
        this IServiceCollection services,
        Action<BatchIngestorSettings> configure)
    {
        var settings = new BatchIngestorSettings();
        configure(settings);

        return services.AddBatchIngestorFactory(settings);
    }
}
