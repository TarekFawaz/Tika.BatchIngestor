namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Provides health check information for batch ingestion operations.
/// </summary>
public interface IBatchIngestorHealthCheckPublisher
{
    /// <summary>
    /// Gets the current health status.
    /// </summary>
    HealthStatus GetHealthStatus();

    /// <summary>
    /// Gets detailed health check result.
    /// </summary>
    HealthCheckResult GetHealthCheckResult();
}

/// <summary>
/// Health status enumeration.
/// </summary>
public enum HealthStatus
{
    /// <summary>
    /// System is healthy and operating normally.
    /// </summary>
    Healthy = 0,

    /// <summary>
    /// System is degraded but still operational.
    /// </summary>
    Degraded = 1,

    /// <summary>
    /// System is unhealthy and may not be functioning properly.
    /// </summary>
    Unhealthy = 2
}

/// <summary>
/// Detailed health check result.
/// </summary>
public class HealthCheckResult
{
    /// <summary>
    /// Overall health status.
    /// </summary>
    public HealthStatus Status { get; set; }

    /// <summary>
    /// Human-readable description of the health status.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Current metrics snapshot.
    /// </summary>
    public BatchIngestMetrics? Metrics { get; set; }

    /// <summary>
    /// Current performance snapshot.
    /// </summary>
    public PerformanceSnapshot? Performance { get; set; }

    /// <summary>
    /// Additional health check data.
    /// </summary>
    public Dictionary<string, object> Data { get; set; } = new();

    /// <summary>
    /// Exception information if unhealthy.
    /// </summary>
    public Exception? Exception { get; set; }

    /// <summary>
    /// Timestamp of the health check.
    /// </summary>
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
