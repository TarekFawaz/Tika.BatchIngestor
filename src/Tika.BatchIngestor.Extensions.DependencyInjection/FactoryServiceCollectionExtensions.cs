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
            return new BatchIngestorFactory(loggerFactory);
        });

        return services;
    }
}
