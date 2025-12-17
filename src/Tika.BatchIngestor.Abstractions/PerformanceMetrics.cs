using System.Diagnostics;

namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Real-time performance metrics including CPU and memory usage.
/// </summary>
public class PerformanceMetrics
{
    private readonly Process _currentProcess = Process.GetCurrentProcess();
    private DateTime _lastCpuCheck = DateTime.UtcNow;
    private TimeSpan _lastTotalProcessorTime;
    private double _cpuUsagePercent;

    public PerformanceMetrics()
    {
        _lastTotalProcessorTime = _currentProcess.TotalProcessorTime;
    }

    /// <summary>
    /// Current CPU usage percentage (0-100).
    /// </summary>
    public double CpuUsagePercent
    {
        get
        {
            RefreshCpuUsage();
            return _cpuUsagePercent;
        }
    }

    /// <summary>
    /// Current working set memory in bytes.
    /// </summary>
    public long WorkingSetBytes => _currentProcess.WorkingSet64;

    /// <summary>
    /// Current working set memory in megabytes.
    /// </summary>
    public double WorkingSetMB => WorkingSetBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Current private memory in bytes.
    /// </summary>
    public long PrivateMemoryBytes => _currentProcess.PrivateMemorySize64;

    /// <summary>
    /// Current private memory in megabytes.
    /// </summary>
    public double PrivateMemoryMB => PrivateMemoryBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Peak working set memory in bytes.
    /// </summary>
    public long PeakWorkingSetBytes => _currentProcess.PeakWorkingSet64;

    /// <summary>
    /// Peak working set memory in megabytes.
    /// </summary>
    public double PeakWorkingSetMB => PeakWorkingSetBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Number of garbage collections for generation 0.
    /// </summary>
    public int Gen0Collections => GC.CollectionCount(0);

    /// <summary>
    /// Number of garbage collections for generation 1.
    /// </summary>
    public int Gen1Collections => GC.CollectionCount(1);

    /// <summary>
    /// Number of garbage collections for generation 2.
    /// </summary>
    public int Gen2Collections => GC.CollectionCount(2);

    /// <summary>
    /// Total allocated bytes (available in .NET 6+).
    /// </summary>
    public long TotalAllocatedBytes => GC.GetTotalAllocatedBytes();

    /// <summary>
    /// Total memory allocated in megabytes.
    /// </summary>
    public double TotalAllocatedMB => TotalAllocatedBytes / (1024.0 * 1024.0);

    /// <summary>
    /// Number of active threads in the process.
    /// </summary>
    public int ThreadCount
    {
        get
        {
            _currentProcess.Refresh();
            return _currentProcess.Threads.Count;
        }
    }

    /// <summary>
    /// Refreshes the CPU usage calculation.
    /// Should be called periodically for accurate readings.
    /// </summary>
    private void RefreshCpuUsage()
    {
        var now = DateTime.UtcNow;
        var elapsed = (now - _lastCpuCheck).TotalMilliseconds;

        // Only refresh if at least 500ms has passed
        if (elapsed < 500)
            return;

        _currentProcess.Refresh();
        var currentTotalProcessorTime = _currentProcess.TotalProcessorTime;
        var cpuUsed = (currentTotalProcessorTime - _lastTotalProcessorTime).TotalMilliseconds;
        var cpuUsagePercentage = (cpuUsed / (Environment.ProcessorCount * elapsed)) * 100.0;

        _cpuUsagePercent = Math.Min(100.0, Math.Max(0.0, cpuUsagePercentage));
        _lastCpuCheck = now;
        _lastTotalProcessorTime = currentTotalProcessorTime;
    }

    /// <summary>
    /// Creates a snapshot of current performance metrics.
    /// </summary>
    public PerformanceSnapshot CreateSnapshot()
    {
        RefreshCpuUsage();
        return new PerformanceSnapshot
        {
            CpuUsagePercent = _cpuUsagePercent,
            WorkingSetMB = WorkingSetMB,
            PrivateMemoryMB = PrivateMemoryMB,
            PeakWorkingSetMB = PeakWorkingSetMB,
            Gen0Collections = Gen0Collections,
            Gen1Collections = Gen1Collections,
            Gen2Collections = Gen2Collections,
            TotalAllocatedMB = TotalAllocatedMB,
            ThreadCount = ThreadCount,
            Timestamp = DateTime.UtcNow
        };
    }
}

/// <summary>
/// Immutable snapshot of performance metrics at a point in time.
/// </summary>
public class PerformanceSnapshot
{
    public double CpuUsagePercent { get; init; }
    public double WorkingSetMB { get; init; }
    public double PrivateMemoryMB { get; init; }
    public double PeakWorkingSetMB { get; init; }
    public int Gen0Collections { get; init; }
    public int Gen1Collections { get; init; }
    public int Gen2Collections { get; init; }
    public double TotalAllocatedMB { get; init; }
    public int ThreadCount { get; init; }
    public DateTime Timestamp { get; init; }

    public override string ToString()
    {
        return $"CPU: {CpuUsagePercent:F2}%, Memory: {WorkingSetMB:F2}MB, GC: Gen0={Gen0Collections}, Gen1={Gen1Collections}, Gen2={Gen2Collections}, Threads: {ThreadCount}";
    }
}
