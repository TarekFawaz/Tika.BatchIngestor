using Microsoft.Extensions.Diagnostics.HealthChecks;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.HealthChecks;

/// <summary>
/// ASP.NET Core Health Check integration for BatchIngestor.
/// Usage: services.AddHealthChecks().AddCheck&lt;BatchIngestorHealthCheck&gt;("batch-ingestor");
/// </summary>
public class BatchIngestorHealthCheck : IHealthCheck
{
    private readonly IHealthCheckPublisher _healthCheckPublisher;

    public BatchIngestorHealthCheck(IHealthCheckPublisher healthCheckPublisher)
    {
        _healthCheckPublisher = healthCheckPublisher ?? throw new ArgumentNullException(nameof(healthCheckPublisher));
    }

    public Task<Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var result = _healthCheckPublisher.GetHealthCheckResult();

        var aspnetStatus = result.Status switch
        {
            Abstractions.HealthStatus.Healthy => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Healthy,
            Abstractions.HealthStatus.Degraded => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Degraded,
            Abstractions.HealthStatus.Unhealthy => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy,
            _ => Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus.Unhealthy
        };

        var healthCheckResult = new Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult(
            status: aspnetStatus,
            description: result.Description,
            exception: result.Exception,
            data: result.Data.AsReadOnly()
        );

        return Task.FromResult(healthCheckResult);
    }
}

/// <summary>
/// Extension methods for registering BatchIngestor health checks.
/// </summary>
public static class BatchIngestorHealthCheckExtensions
{
    /// <summary>
    /// Adds a health check for BatchIngestor operations.
    /// </summary>
    public static IHealthChecksBuilder AddBatchIngestorHealthCheck(
        this IHealthChecksBuilder builder,
        string name = "batch-ingestor",
        Microsoft.Extensions.Diagnostics.HealthChecks.HealthStatus? failureStatus = null,
        IEnumerable<string>? tags = null,
        TimeSpan? timeout = null)
    {
        return builder.AddCheck<BatchIngestorHealthCheck>(
            name,
            failureStatus,
            tags,
            timeout);
    }
}
