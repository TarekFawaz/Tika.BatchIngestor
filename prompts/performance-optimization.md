# Performance Optimization Guide for Contributors

Use this prompt when working on performance improvements or analyzing bottlenecks.

## Task

I'm working on optimizing performance for Tika.BatchIngestor. Help me analyze and improve [SPECIFIC_AREA].

### Optimization Goals

- **Target Throughput**: 5,000-15,000 rows/sec
- **CPU Usage Target**: < 80% (configurable, default)
- **Memory Pressure**: Minimal GC Gen2 collections
- **Latency**: < 100ms average batch processing time

### Current Performance Characteristics

The library has been optimized with the following patterns:

1. **Lock-Free Metrics Collection**
   - `Interlocked` operations instead of locks
   - Location: `BatchIngestMetrics.cs`
   - Impact: Eliminates contention in high-parallelism scenarios

2. **Zero-Allocation List Segmentation**
   - `ListSegment<T>` struct avoids `Skip().Take().ToList()`
   - Location: `GenericInsertStrategy.cs`
   - Impact: Reduces GC pressure by ~30%

3. **Optimized Task.Yield Pattern**
   - Periodic yielding (every 1000 items) vs per-item
   - Location: `BatchIngestor.cs` - `ToAsyncEnumerable()`
   - Impact: Reduces context switches by 99%

4. **CPU Throttling**
   - Automatic backoff when CPU exceeds threshold
   - Location: `BatchProcessor.cs` - `ProcessBatchAsync()`
   - Impact: Prevents system overload

5. **Pre-Allocated Collections**
   - Lists created with capacity
   - Location: `BatchProcessor.cs` - `ProduceAsync()`
   - Impact: Reduces List resizing allocations

### Performance Analysis Workflow

1. **Profile Current Performance**
   ```bash
   # Run benchmarks
   cd benchmarks/Tika.BatchIngestor.Benchmarks
   dotnet run -c Release

   # Use dotnet-trace for detailed profiling
   dotnet trace collect --process-id <pid> --providers Microsoft-Windows-DotNETRuntime
   ```

2. **Identify Bottlenecks**
   - CPU: Look for hot paths in profiler
   - Memory: Check GC collections and allocations
   - I/O: Analyze database wait times
   - Lock contention: Check for synchronized blocks

3. **Apply Optimizations**
   - Use `Span<T>` and `Memory<T>` for zero-copy scenarios
   - Use `ArrayPool<T>` for temporary buffers
   - Use `StringBuilder` pooling for SQL generation
   - Consider `ValueTask<T>` for hot paths
   - Use `[MethodImpl(MethodImplOptions.AggressiveInlining)]` for small methods

### Common Optimization Patterns

#### Pattern 1: Replace Lock with Interlocked

**Before:**
```csharp
lock (_metrics)
{
    _metrics.TotalRowsProcessed += count;
    _metrics.BatchesCompleted++;
}
```

**After:**
```csharp
Interlocked.Add(ref _totalRowsProcessed, count);
Interlocked.Increment(ref _batchesCompleted);
```

#### Pattern 2: Use ArrayPool for Temporary Buffers

**Before:**
```csharp
var buffer = new byte[size];
// Use buffer
// Buffer gets GC'd
```

**After:**
```csharp
var buffer = ArrayPool<byte>.Shared.Rent(size);
try
{
    // Use buffer
}
finally
{
    ArrayPool<byte>.Shared.Return(buffer);
}
```

#### Pattern 3: Avoid LINQ in Hot Paths

**Before:**
```csharp
var chunk = rows.Skip(i).Take(chunkSize).ToList();
```

**After:**
```csharp
var chunk = new ListSegment<T>(rows, i, chunkSize); // Zero-allocation view
```

#### Pattern 4: Use ValueTask for Synchronous Returns

**Before:**
```csharp
public async Task<int> GetCountAsync()
{
    return _count; // Allocates Task<int>
}
```

**After:**
```csharp
public ValueTask<int> GetCountAsync()
{
    return new ValueTask<int>(_count); // No allocation if synchronous
}
```

### Benchmarking Requirements

All performance improvements must include:

1. **Before/After Benchmarks**
   ```csharp
   [MemoryDiagnoser]
   [ThreadingDiagnoser]
   public class OptimizationBenchmark
   {
       [Benchmark(Baseline = true)]
       public void Original() { /* ... */ }

       [Benchmark]
       public void Optimized() { /* ... */ }
   }
   ```

2. **Metrics to Measure**
   - Mean execution time
   - Memory allocation (Gen0/Gen1/Gen2 collections)
   - Throughput (rows/sec)
   - CPU usage
   - Thread count

3. **Test Scenarios**
   - Small batches (500 rows)
   - Medium batches (1000 rows)
   - Large batches (5000 rows)
   - Various parallelism levels (2, 4, 8)

### Performance Testing Checklist

- [ ] Run `dotnet run -c Release` (never Debug)
- [ ] Test with representative data sizes
- [ ] Measure throughput (rows/sec)
- [ ] Measure CPU usage (should stay < 80% with throttling enabled)
- [ ] Check GC collections (Gen2 should be minimal)
- [ ] Verify memory usage (no memory leaks)
- [ ] Test with different databases (local, remote, cloud)
- [ ] Validate no regressions in existing scenarios

### Profiling Tools

1. **BenchmarkDotNet**: Microbenchmarks with statistical analysis
2. **dotnet-trace**: Event-based profiling
3. **dotnet-counters**: Real-time performance metrics
4. **PerfView**: Windows performance analysis
5. **Visual Studio Profiler**: CPU/Memory profiling

### Submitting Performance Improvements

When submitting a PR with performance improvements:

1. Include benchmark results showing improvement
2. Explain the optimization technique used
3. Document any trade-offs (e.g., increased complexity)
4. Ensure all tests pass
5. Add new tests if behavior changed
6. Update documentation if public API changed

## Example PR Description

```markdown
## Performance Improvement: [Description]

### Problem
[Describe the bottleneck or performance issue]

### Solution
[Explain the optimization applied]

### Results

| Scenario | Before | After | Improvement |
|----------|--------|-------|-------------|
| Small rows (500) | 5,234 rows/sec | 8,721 rows/sec | +66% |
| Medium rows (1000) | 4,892 rows/sec | 7,543 rows/sec | +54% |
| Large rows (5000) | 4,123 rows/sec | 6,234 rows/sec | +51% |

**Memory Impact:**
- Gen0: 245 → 123 (-50%)
- Gen1: 12 → 5 (-58%)
- Gen2: 3 → 1 (-67%)

**CPU Usage:** 72% → 65% (-7 percentage points)

### Trade-offs
[List any trade-offs, e.g., increased code complexity, additional dependencies]
```

Please follow these guidelines when working on performance optimizations.
