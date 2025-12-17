# Testing and Benchmarking Guide

Use this prompt when writing tests or benchmarks for Tika.BatchIngestor.

## Task

I need to [write tests / create benchmarks / validate performance] for [FEATURE_NAME].

## Testing Guidelines

### Unit Testing Standards

1. **Test Organization**
   - One test class per production class
   - Location: `tests/Tika.BatchIngestor.Tests/`
   - Naming: `[ClassName]Tests.cs`

2. **Test Method Naming**
   ```csharp
   [Fact]
   public void MethodName_Scenario_ExpectedBehavior()
   {
       // Arrange
       // Act
       // Assert
   }
   ```

3. **Required Test Categories**
   - **Happy path**: Normal, successful execution
   - **Edge cases**: Empty inputs, boundary values
   - **Error handling**: Invalid inputs, exceptions
   - **Concurrency**: Thread-safe operations (if applicable)

### Example Unit Test

```csharp
using Xunit;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Tests;

public class BatchIngestMetricsTests
{
    [Fact]
    public void AddRowsProcessed_IncrementsCounter_ThreadSafe()
    {
        // Arrange
        var metrics = new BatchIngestMetrics();
        var tasks = new List<Task>();

        // Act - concurrent increments
        for (int i = 0; i < 10; i++)
        {
            tasks.Add(Task.Run(() =>
            {
                for (int j = 0; j < 1000; j++)
                {
                    metrics.AddRowsProcessed(1);
                }
            }));
        }
        Task.WaitAll(tasks.ToArray());

        // Assert
        Assert.Equal(10_000, metrics.TotalRowsProcessed);
    }

    [Fact]
    public void RecordBatchDuration_UpdatesMinMaxCorrectly()
    {
        // Arrange
        var metrics = new BatchIngestMetrics();

        // Act
        metrics.RecordBatchDuration(TimeSpan.FromMilliseconds(100));
        metrics.RecordBatchDuration(TimeSpan.FromMilliseconds(200));
        metrics.RecordBatchDuration(TimeSpan.FromMilliseconds(150));

        // Assert
        Assert.Equal(TimeSpan.FromMilliseconds(100), metrics.MinBatchDuration);
        Assert.Equal(TimeSpan.FromMilliseconds(200), metrics.MaxBatchDuration);
    }

    [Fact]
    public void Clone_CreatesIndependentCopy()
    {
        // Arrange
        var original = new BatchIngestMetrics();
        original.AddRowsProcessed(1000);

        // Act
        var clone = original.Clone();
        clone.AddRowsProcessed(500);

        // Assert
        Assert.Equal(1000, original.TotalRowsProcessed);
        Assert.Equal(1500, clone.TotalRowsProcessed);
    }
}
```

### Integration Testing with Real Databases

```csharp
public class BatchIngestorIntegrationTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly string _connectionString;

    public BatchIngestorIntegrationTests()
    {
        _connectionString = $"Data Source=test_{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();
        SetupTestTable();
    }

    [Fact]
    public async Task IngestAsync_InsertsAllRows_Successfully()
    {
        // Arrange
        var data = GenerateTestData(1000);
        var options = new BatchIngestOptions
        {
            BatchSize = 100,
            MaxDegreeOfParallelism = 2
        };

        var ingestor = CreateIngestor(options);

        // Act
        var metrics = await ingestor.IngestAsync(data, "TestTable");

        // Assert
        Assert.Equal(1000, metrics.TotalRowsProcessed);
        Assert.Equal(10, metrics.BatchesCompleted);

        // Verify data in database
        var count = await GetRowCount();
        Assert.Equal(1000, count);
    }

    public void Dispose()
    {
        _connection?.Close();
        _connection?.Dispose();
    }
}
```

## Benchmarking Guidelines

### Benchmark Structure

```csharp
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;

[Config(typeof(Config))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class FeatureBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
                .WithWarmupCount(3)      // Warm up iterations
                .WithIterationCount(5)   // Measurement iterations
                .WithInvocationCount(1)  // Invocations per iteration
                .WithUnrollFactor(1));   // Loop unroll factor
        }
    }

    [Params(1000, 5000, 10000)]
    public int RowCount { get; set; }

    [Params(500, 1000, 2000)]
    public int BatchSize { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        // Initialize resources (connections, test data, etc.)
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        // Clean up resources
    }

    [Benchmark(Baseline = true)]
    public async Task<BatchIngestMetrics> Baseline()
    {
        // Baseline implementation
    }

    [Benchmark]
    public async Task<BatchIngestMetrics> Optimized()
    {
        // Optimized implementation
    }
}
```

### Benchmark Best Practices

1. **Use Release Configuration**
   ```bash
   dotnet run -c Release
   ```

2. **Parameterize Variables**
   - Row counts: 1000, 5000, 10000, 15000
   - Batch sizes: 500, 1000, 2000
   - Parallelism: 2, 4, 8

3. **Measure Multiple Metrics**
   - Execution time (Mean, Median, P95)
   - Memory allocation (Gen0/Gen1/Gen2)
   - Throughput (rows/sec)
   - CPU usage
   - Thread count

4. **Use Realistic Data**
   - Vary row sizes (small, medium, large)
   - Include NULL values
   - Test with different data types

### IoT-Specific Benchmark Scenarios

```csharp
[Benchmark(Description = "IoT Sensor Data (100 bytes/row, high volume)")]
public async Task<BatchIngestMetrics> IngestIoTSensorData()
{
    var sensorReadings = GenerateSensorReadings(RowCount);
    return await IngestData(sensorReadings, "SensorReadings");
}

[Benchmark(Description = "Vehicle Telemetry (500 bytes/row, medium volume)")]
public async Task<BatchIngestMetrics> IngestVehicleTelemetry()
{
    var telemetry = GenerateVehicleTelemetry(RowCount);
    return await IngestData(telemetry, "VehicleTelemetry");
}

[Benchmark(Description = "Time-Series Metrics (64 bytes/row, very high volume)")]
public async Task<BatchIngestMetrics> IngestTimeSeriesMetrics()
{
    var metrics = GenerateTimeSeriesMetrics(RowCount);
    return await IngestData(metrics, "TimeSeriesMetrics");
}
```

### Running Benchmarks

```bash
# Run all benchmarks
cd benchmarks/Tika.BatchIngestor.Benchmarks
dotnet run -c Release

# Run specific benchmark
dotnet run -c Release --filter "*IngestSensorReadings*"

# Export results
dotnet run -c Release --exporters json,html,csv
```

### Interpreting Benchmark Results

```
| Method               | RowCount | BatchSize | Mean      | StdDev | Gen0   | Gen1  | Gen2  | Allocated |
|----------------------|----------|-----------|-----------|--------|--------|-------|-------|-----------|
| IngestSensorReadings | 10000    | 1000      | 1.234 s   | 0.042s | 245.1  | 12.3  | 3.2   | 1.23 MB   |
| IngestVehicleTelemetry| 10000   | 1000      | 1.567 s   | 0.038s | 312.5  | 15.7  | 4.1   | 1.89 MB   |
```

**Analysis:**
- **Mean**: Average execution time (lower is better)
- **StdDev**: Consistency (lower is better)
- **Gen0/1/2**: GC collections (lower is better, especially Gen2)
- **Allocated**: Total memory allocated (lower is better)

**Performance Targets:**
- Throughput: 5,000-15,000 rows/sec
- CPU: < 80% (with throttling enabled)
- Gen2 Collections: < 5 per 100k rows
- Memory: Linear with batch size, not total row count

### Performance Regression Testing

Add benchmarks to CI/CD to catch regressions:

```yaml
# .github/workflows/benchmark.yml
- name: Run Benchmarks
  run: |
    cd benchmarks/Tika.BatchIngestor.Benchmarks
    dotnet run -c Release --exporters json

- name: Compare Results
  run: |
    # Compare with baseline
    # Fail if regression > 10%
```

### Test Coverage Requirements

Aim for:
- **Line Coverage**: > 80%
- **Branch Coverage**: > 70%
- **Critical Paths**: 100% (metrics, health checks, core ingestion)

```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coverage-report" -reporttypes:Html
```

## Checklist for Test PRs

- [ ] All tests pass locally
- [ ] Added tests for new functionality
- [ ] Added tests for edge cases
- [ ] Added tests for error conditions
- [ ] Verified thread safety (if applicable)
- [ ] Benchmarks show no performance regression
- [ ] Test names follow naming convention
- [ ] Tests are isolated and don't depend on execution order
- [ ] Dispose resources properly (IDisposable)
- [ ] Tests run quickly (< 1 second per test, typically)

## Common Testing Patterns

### Pattern 1: Test with Fake/Mock Objects

```csharp
public class FakeConnectionFactory : IConnectionFactory
{
    public int ConnectionsCreated { get; private set; }

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken ct)
    {
        ConnectionsCreated++;
        var connection = new FakeDbConnection();
        await connection.OpenAsync(ct);
        return connection;
    }
}
```

### Pattern 2: Test Async Operations

```csharp
[Fact]
public async Task ProcessAsync_CompletesConcurrently()
{
    var processor = CreateProcessor();
    var sw = Stopwatch.StartNew();

    await processor.ProcessAsync(data, CancellationToken.None);

    sw.Stop();
    // With parallelism=4, should be ~4x faster than sequential
    Assert.True(sw.Elapsed < sequentialTime / 3);
}
```

### Pattern 3: Test Cancellation

```csharp
[Fact]
public async Task IngestAsync_RespectsCancellation()
{
    var cts = new CancellationTokenSource();
    var data = GenerateInfiniteData(); // Never-ending stream

    var task = ingestor.IngestAsync(data, "Table", cts.Token);

    await Task.Delay(100);
    cts.Cancel();

    await Assert.ThrowsAsync<OperationCanceledException>(() => task);
}
```

Please follow these guidelines when writing tests and benchmarks.
