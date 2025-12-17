using Microsoft.Extensions.Logging;

namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Configuration options for batch ingestion.
/// </summary>
public class BatchIngestOptions
{
    /// <summary>
    /// Number of rows per batch. Default is 1000.
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Maximum number of concurrent insert operations. Default is ProcessorCount.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Maximum number of batches queued for processing (backpressure control).
    /// This indirectly controls memory usage. Default is 10.
    /// Formula: Max Memory ≈ MaxInFlightBatches × BatchSize × AvgRowSize
    /// </summary>
    public int MaxInFlightBatches { get; set; } = 10;

    /// <summary>
    /// Command timeout in seconds. Default is 300 (5 minutes).
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Whether to use transactions. Default is true.
    /// </summary>
    public bool UseTransactions { get; set; } = true;

    /// <summary>
    /// Whether to use a separate transaction per batch (true) or one transaction for all data (false).
    /// Default is true. Recommended for large ingests to avoid huge transaction logs.
    /// </summary>
    public bool TransactionPerBatch { get; set; } = true;

    /// <summary>
    /// Retry policy for transient failures. If null, no retries are performed.
    /// </summary>
    public RetryPolicy? RetryPolicy { get; set; }

    /// <summary>
    /// Callback invoked periodically with progress metrics.
    /// </summary>
    public Action<BatchIngestMetrics>? OnProgress { get; set; }

    /// <summary>
    /// Callback invoked when each batch completes.
    /// Parameters: batchNumber, batchDuration
    /// </summary>
    public Action<int, TimeSpan>? OnBatchCompleted { get; set; }

    /// <summary>
    /// Optional logger for diagnostic messages.
    /// </summary>
    public ILogger? Logger { get; set; }

    /// <summary>
    /// Enable CPU usage monitoring and throttling. Default is true.
    /// </summary>
    public bool EnableCpuThrottling { get; set; } = true;

    /// <summary>
    /// Maximum CPU usage percentage threshold (0-100).
    /// When exceeded, processing will throttle. Default is 80%.
    /// Set to 0 to disable throttling.
    /// </summary>
    public double MaxCpuPercent { get; set; } = 80.0;

    /// <summary>
    /// Throttle delay in milliseconds when CPU exceeds threshold.
    /// Default is 100ms.
    /// </summary>
    public int ThrottleDelayMs { get; set; } = 100;

    /// <summary>
    /// Enable real-time performance metrics collection (CPU, memory).
    /// Default is true.
    /// </summary>
    public bool EnablePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Interval in milliseconds to collect performance metrics.
    /// Default is 1000ms (1 second).
    /// </summary>
    public int PerformanceMetricsIntervalMs { get; set; } = 1000;

    /// <summary>
    /// Validates the options and throws if invalid.
    /// </summary>
    public void Validate()
    {
        if (BatchSize <= 0)
            throw new ArgumentException("BatchSize must be greater than 0.", nameof(BatchSize));

        if (MaxDegreeOfParallelism <= 0)
            throw new ArgumentException("MaxDegreeOfParallelism must be greater than 0.", nameof(MaxDegreeOfParallelism));

        if (MaxInFlightBatches <= 0)
            throw new ArgumentException("MaxInFlightBatches must be greater than 0.", nameof(MaxInFlightBatches));

        if (CommandTimeoutSeconds < 0)
            throw new ArgumentException("CommandTimeoutSeconds cannot be negative.", nameof(CommandTimeoutSeconds));

        if (MaxCpuPercent < 0 || MaxCpuPercent > 100)
            throw new ArgumentException("MaxCpuPercent must be between 0 and 100.", nameof(MaxCpuPercent));

        if (ThrottleDelayMs < 0)
            throw new ArgumentException("ThrottleDelayMs cannot be negative.", nameof(ThrottleDelayMs));

        if (PerformanceMetricsIntervalMs <= 0)
            throw new ArgumentException("PerformanceMetricsIntervalMs must be greater than 0.", nameof(PerformanceMetricsIntervalMs));
    }
}
