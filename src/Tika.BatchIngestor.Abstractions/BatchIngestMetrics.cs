namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Metrics collected during a batch ingestion operation.
/// </summary>
public class BatchIngestMetrics
{
    /// <summary>
    /// Total number of rows successfully processed.
    /// </summary>
    public long TotalRowsProcessed { get; set; }

    /// <summary>
    /// Total number of batches completed.
    /// </summary>
    public int BatchesCompleted { get; set; }

    /// <summary>
    /// Total elapsed time for the ingestion.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }

    /// <summary>
    /// Average rows processed per second.
    /// </summary>
    public double RowsPerSecond => ElapsedTime.TotalSeconds > 0
        ? TotalRowsProcessed / ElapsedTime.TotalSeconds
        : 0;

    /// <summary>
    /// Total number of errors encountered (before retries).
    /// </summary>
    public int ErrorCount { get; set; }

    /// <summary>
    /// Total number of retry attempts.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Minimum batch duration observed.
    /// </summary>
    public TimeSpan MinBatchDuration { get; set; } = TimeSpan.MaxValue;

    /// <summary>
    /// Maximum batch duration observed.
    /// </summary>
    public TimeSpan MaxBatchDuration { get; set; } = TimeSpan.MinValue;

    /// <summary>
    /// Average batch duration.
    /// </summary>
    public TimeSpan AverageBatchDuration { get; set; }

    /// <summary>
    /// Creates a snapshot of the current metrics.
    /// </summary>
    public BatchIngestMetrics Clone()
    {
        return new BatchIngestMetrics
        {
            TotalRowsProcessed = TotalRowsProcessed,
            BatchesCompleted = BatchesCompleted,
            ElapsedTime = ElapsedTime,
            ErrorCount = ErrorCount,
            RetryCount = RetryCount,
            MinBatchDuration = MinBatchDuration,
            MaxBatchDuration = MaxBatchDuration,
            AverageBatchDuration = AverageBatchDuration
        };
    }
}
