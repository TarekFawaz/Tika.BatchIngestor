using BenchmarkDotNet.Attributes;
using Microsoft.Data.Sqlite;
using Tika.BatchIngestor;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Dialects;
using Tika.BatchIngestor.Factories;

namespace Tika.BatchIngestor.Benchmarks;

/// <summary>
/// Benchmarks comparing different configuration scenarios to validate
/// CPU throttling, throughput targets, and memory optimizations.
/// </summary>
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class PerformanceComparisonBenchmarks
{
    private string _connectionString = string.Empty;
    private SqliteConnection? _connection;
    private const int TargetRowCount = 10000;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _connectionString = $"Data Source=perf_{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS TestData (
                Id TEXT,
                Timestamp TEXT,
                Value REAL,
                Description TEXT
            )";
        cmd.ExecuteNonQuery();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    [Benchmark(Baseline = true, Description = "Default Configuration")]
    public async Task<BatchIngestMetrics> DefaultConfiguration()
    {
        var options = new BatchIngestOptions
        {
            BatchSize = 1000,
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            MaxInFlightBatches = 10,
            UseTransactions = true,
            TransactionPerBatch = true,
            EnableCpuThrottling = false,
            EnablePerformanceMetrics = true
        };

        return await RunIngestion(options);
    }

    [Benchmark(Description = "High Throughput (Target: 5000-15000 rows/sec)")]
    public async Task<BatchIngestMetrics> HighThroughputConfiguration()
    {
        var options = new BatchIngestOptions
        {
            BatchSize = 2000,
            MaxDegreeOfParallelism = 8,
            MaxInFlightBatches = 20,
            UseTransactions = true,
            TransactionPerBatch = true,
            EnableCpuThrottling = false,
            EnablePerformanceMetrics = true
        };

        return await RunIngestion(options);
    }

    [Benchmark(Description = "CPU Throttled (Max 80% CPU)")]
    public async Task<BatchIngestMetrics> CpuThrottledConfiguration()
    {
        var options = new BatchIngestOptions
        {
            BatchSize = 1000,
            MaxDegreeOfParallelism = 4,
            MaxInFlightBatches = 10,
            UseTransactions = true,
            TransactionPerBatch = true,
            EnableCpuThrottling = true,
            MaxCpuPercent = 80.0,
            ThrottleDelayMs = 100,
            EnablePerformanceMetrics = true
        };

        return await RunIngestion(options);
    }

    [Benchmark(Description = "Memory Optimized (Low GC pressure)")]
    public async Task<BatchIngestMetrics> MemoryOptimizedConfiguration()
    {
        var options = new BatchIngestOptions
        {
            BatchSize = 500,
            MaxDegreeOfParallelism = 4,
            MaxInFlightBatches = 5,
            UseTransactions = true,
            TransactionPerBatch = true,
            EnableCpuThrottling = false,
            EnablePerformanceMetrics = true
        };

        return await RunIngestion(options);
    }

    [Benchmark(Description = "Balanced (Production-ready)")]
    public async Task<BatchIngestMetrics> BalancedConfiguration()
    {
        var options = new BatchIngestOptions
        {
            BatchSize = 1000,
            MaxDegreeOfParallelism = 6,
            MaxInFlightBatches = 10,
            UseTransactions = true,
            TransactionPerBatch = true,
            EnableCpuThrottling = true,
            MaxCpuPercent = 85.0,
            ThrottleDelayMs = 50,
            EnablePerformanceMetrics = true
        };

        return await RunIngestion(options);
    }

    private async Task<BatchIngestMetrics> RunIngestion(BatchIngestOptions options)
    {
        var data = GenerateTestData(TargetRowCount);
        var factory = new SimpleConnectionFactory(_connectionString, () => new SqliteConnection(_connectionString));
        var dialect = new GenericSqlDialect();
        var mapper = new DefaultRowMapper<TestData>(MapTestData);

        var ingestor = new BatchIngestor<TestData>(factory, dialect, mapper, options);
        return await ingestor.IngestAsync(data, "TestData");
    }

    private static List<TestData> GenerateTestData(int count)
    {
        var data = new List<TestData>(count);
        var random = new Random(42);
        var baseTime = DateTime.UtcNow;

        for (int i = 0; i < count; i++)
        {
            data.Add(new TestData
            {
                Id = Guid.NewGuid().ToString(),
                Timestamp = baseTime.AddSeconds(i),
                Value = random.NextDouble() * 1000,
                Description = $"Test data item {i}"
            });
        }

        return data;
    }

    private static Dictionary<string, object?> MapTestData(TestData td) => new()
    {
        ["Id"] = td.Id,
        ["Timestamp"] = td.Timestamp.ToString("O"),
        ["Value"] = td.Value,
        ["Description"] = td.Description
    };

    private class TestData
    {
        public string Id { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Description { get; set; } = string.Empty;
    }
}
