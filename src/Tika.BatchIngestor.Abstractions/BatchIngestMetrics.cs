namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Metrics collected during a batch ingestion operation.
/// Thread-safe using atomic operations for lock-free performance.
/// </summary>
public class BatchIngestMetrics
{
    private long _totalRowsProcessed;
    private int _batchesCompleted;
    private int _errorCount;
    private int _retryCount;
    private long _totalBatchDurationTicks;
    private long _minBatchDurationTicks = long.MaxValue;
    private long _maxBatchDurationTicks = long.MinValue;

    /// <summary>
    /// Total number of rows successfully processed.
    /// </summary>
    public long TotalRowsProcessed => Interlocked.Read(ref _totalRowsProcessed);

    /// <summary>
    /// Total number of batches completed.
    /// </summary>
    public int BatchesCompleted => Interlocked.CompareExchange(ref _batchesCompleted, 0, 0);

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
    public int ErrorCount => Interlocked.CompareExchange(ref _errorCount, 0, 0);

    /// <summary>
    /// Total number of retry attempts.
    /// </summary>
    public int RetryCount => Interlocked.CompareExchange(ref _retryCount, 0, 0);

    /// <summary>
    /// Minimum batch duration observed.
    /// </summary>
    public TimeSpan MinBatchDuration
    {
        get
        {
            var ticks = Interlocked.Read(ref _minBatchDurationTicks);
            return ticks == long.MaxValue ? TimeSpan.MaxValue : TimeSpan.FromTicks(ticks);
        }
    }

    /// <summary>
    /// Maximum batch duration observed.
    /// </summary>
    public TimeSpan MaxBatchDuration
    {
        get
        {
            var ticks = Interlocked.Read(ref _maxBatchDurationTicks);
            return ticks == long.MinValue ? TimeSpan.MinValue : TimeSpan.FromTicks(ticks);
        }
    }

    /// <summary>
    /// Average batch duration.
    /// </summary>
    public TimeSpan AverageBatchDuration
    {
        get
        {
            var batches = BatchesCompleted;
            if (batches == 0) return TimeSpan.Zero;
            var totalTicks = Interlocked.Read(ref _totalBatchDurationTicks);
            return TimeSpan.FromTicks(totalTicks / batches);
        }
    }

    /// <summary>
    /// Current performance snapshot (CPU, memory).
    /// </summary>
    public PerformanceSnapshot? CurrentPerformance { get; set; }

    /// <summary>
    /// Peak performance snapshot observed during ingestion.
    /// </summary>
    public PerformanceSnapshot? PeakPerformance { get; set; }

    /// <summary>
    /// Increments the total rows processed counter.
    /// </summary>
    internal void AddRowsProcessed(long count)
    {
        Interlocked.Add(ref _totalRowsProcessed, count);
    }

    /// <summary>
    /// Increments the batches completed counter.
    /// </summary>
    internal void IncrementBatchesCompleted()
    {
        Interlocked.Increment(ref _batchesCompleted);
    }

    /// <summary>
    /// Increments the error counter.
    /// </summary>
    internal void IncrementErrorCount()
    {
        Interlocked.Increment(ref _errorCount);
    }

    /// <summary>
    /// Increments the retry counter.
    /// </summary>
    internal void IncrementRetryCount()
    {
        Interlocked.Increment(ref _retryCount);
    }

    /// <summary>
    /// Records a batch duration using lock-free atomic operations.
    /// </summary>
    internal void RecordBatchDuration(TimeSpan duration)
    {
        var ticks = duration.Ticks;
        Interlocked.Add(ref _totalBatchDurationTicks, ticks);

        // Update min
        long currentMin;
        do
        {
            currentMin = Interlocked.Read(ref _minBatchDurationTicks);
            if (ticks >= currentMin) break;
        } while (Interlocked.CompareExchange(ref _minBatchDurationTicks, ticks, currentMin) != currentMin);

        // Update max
        long currentMax;
        do
        {
            currentMax = Interlocked.Read(ref _maxBatchDurationTicks);
            if (ticks <= currentMax) break;
        } while (Interlocked.CompareExchange(ref _maxBatchDurationTicks, ticks, currentMax) != currentMax);
    }

    /// <summary>
    /// Updates performance snapshot if provided snapshot has higher CPU usage.
    /// </summary>
    internal void UpdatePeakPerformance(PerformanceSnapshot snapshot)
    {
        if (PeakPerformance == null || snapshot.CpuUsagePercent > PeakPerformance.CpuUsagePercent)
        {
            PeakPerformance = snapshot;
        }
        CurrentPerformance = snapshot;
    }

    /// <summary>
    /// Creates a snapshot of the current metrics.
    /// </summary>
    public BatchIngestMetrics Clone()
    {
        return new BatchIngestMetrics
        {
            _totalRowsProcessed = Interlocked.Read(ref _totalRowsProcessed),
            _batchesCompleted = Interlocked.CompareExchange(ref _batchesCompleted, 0, 0),
            ElapsedTime = ElapsedTime,
            _errorCount = Interlocked.CompareExchange(ref _errorCount, 0, 0),
            _retryCount = Interlocked.CompareExchange(ref _retryCount, 0, 0),
            _minBatchDurationTicks = Interlocked.Read(ref _minBatchDurationTicks),
            _maxBatchDurationTicks = Interlocked.Read(ref _maxBatchDurationTicks),
            _totalBatchDurationTicks = Interlocked.Read(ref _totalBatchDurationTicks),
            CurrentPerformance = CurrentPerformance,
            PeakPerformance = PeakPerformance
        };
    }
}
