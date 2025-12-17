using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.HealthChecks;

/// <summary>
/// Default implementation of health check publisher for batch ingestion.
/// </summary>
public class BatchIngestorHealthCheckPublisher : IHealthCheckPublisher
{
    private readonly BatchIngestMetrics _metrics;
    private readonly PerformanceMetrics? _performanceMetrics;
    private readonly BatchIngestOptions _options;

    public BatchIngestorHealthCheckPublisher(
        BatchIngestMetrics metrics,
        PerformanceMetrics? performanceMetrics,
        BatchIngestOptions options)
    {
        _metrics = metrics ?? throw new ArgumentNullException(nameof(metrics));
        _performanceMetrics = performanceMetrics;
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public HealthStatus GetHealthStatus()
    {
        return GetHealthCheckResult().Status;
    }

    public HealthCheckResult GetHealthCheckResult()
    {
        var result = new HealthCheckResult
        {
            Metrics = _metrics.Clone(),
            Timestamp = DateTime.UtcNow
        };

        if (_performanceMetrics != null)
        {
            result.Performance = _performanceMetrics.CreateSnapshot();
        }

        // Determine health status based on metrics
        var errorRate = _metrics.BatchesCompleted > 0
            ? (double)_metrics.ErrorCount / _metrics.BatchesCompleted
            : 0;

        var cpuUsage = result.Performance?.CpuUsagePercent ?? 0;
        var memoryMB = result.Performance?.WorkingSetMB ?? 0;

        // Health status logic
        if (_metrics.ErrorCount > 0 && errorRate > 0.5)
        {
            result.Status = HealthStatus.Unhealthy;
            result.Description = $"High error rate: {errorRate:P2} ({_metrics.ErrorCount} errors in {_metrics.BatchesCompleted} batches)";
        }
        else if (cpuUsage > _options.MaxCpuPercent && _options.EnableCpuThrottling)
        {
            result.Status = HealthStatus.Degraded;
            result.Description = $"CPU usage ({cpuUsage:F2}%) exceeds threshold ({_options.MaxCpuPercent:F2}%)";
        }
        else if (errorRate > 0.1 && errorRate <= 0.5)
        {
            result.Status = HealthStatus.Degraded;
            result.Description = $"Moderate error rate: {errorRate:P2}";
        }
        else if (_metrics.BatchesCompleted > 0 && _metrics.RowsPerSecond < 1000)
        {
            result.Status = HealthStatus.Degraded;
            result.Description = $"Low throughput: {_metrics.RowsPerSecond:N0} rows/sec";
        }
        else
        {
            result.Status = HealthStatus.Healthy;
            result.Description = $"Operating normally. Throughput: {_metrics.RowsPerSecond:N0} rows/sec, CPU: {cpuUsage:F2}%";
        }

        // Add detailed data
        result.Data["TotalRowsProcessed"] = _metrics.TotalRowsProcessed;
        result.Data["BatchesCompleted"] = _metrics.BatchesCompleted;
        result.Data["ErrorCount"] = _metrics.ErrorCount;
        result.Data["ErrorRate"] = errorRate;
        result.Data["RowsPerSecond"] = _metrics.RowsPerSecond;
        result.Data["AverageBatchDuration"] = _metrics.AverageBatchDuration;

        if (result.Performance != null)
        {
            result.Data["CpuUsagePercent"] = result.Performance.CpuUsagePercent;
            result.Data["MemoryMB"] = result.Performance.WorkingSetMB;
            result.Data["PeakMemoryMB"] = result.Performance.PeakWorkingSetMB;
            result.Data["Gen0Collections"] = result.Performance.Gen0Collections;
            result.Data["Gen1Collections"] = result.Performance.Gen1Collections;
            result.Data["Gen2Collections"] = result.Performance.Gen2Collections;
            result.Data["ThreadCount"] = result.Performance.ThreadCount;
        }

        return result;
    }
}
