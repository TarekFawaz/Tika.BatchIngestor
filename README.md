# Tika.BatchIngestor

[![NuGet](https://img.shields.io/nuget/v/Tika.BatchIngestor.svg)](https://www.nuget.org/packages/Tika.BatchIngestor/)
[![Build](https://github.com/TarekFawaz/Tika.BatchIngestor/workflows/CI/badge.svg)](https://github.com/TarekFawaz/Tika.BatchIngestor/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A **high-performance, production-ready** .NET library for efficiently ingesting large volumes of data into relational databases with **optimized CPU usage**, **memory management**, and **real-time health monitoring**.

## 🚀 What is Tika.BatchIngestor?

Tika.BatchIngestor is a lightweight, RDBMS-agnostic library designed to solve the common problem of bulk data insertion into relational databases. It provides:

- **🔥 High Throughput**: 5,000-15,000+ rows/sec with optimized batch processing
- **⚡ CPU Control**: Built-in throttling to keep CPU usage under configurable limits (default: 80%)
- **💾 Memory Optimized**: Lock-free atomic operations, zero-allocation patterns, minimal GC pressure
- **📊 Real-Time Metrics**: CPU, memory, throughput, and performance monitoring
- **🏥 Health Checks**: ASP.NET Core Health Checks integration for production monitoring
- **🌐 Cloud-Ready**: Optimized dialects for Aurora PostgreSQL, Aurora MySQL, and Azure SQL
- **🔒 Resource Control**: Memory and concurrency limits via bounded channels
- **🔌 RDBMS Agnostic**: Works with any ADO.NET provider (SQL Server, PostgreSQL, MySQL, SQLite, etc.)
- **🧩 Extensible**: Plugin architecture for custom dialects and bulk strategies
- **📈 Observable**: Built-in metrics, progress callbacks, and structured logging
- **🛡️ Resilient**: Retry policies with exponential backoff and jitter
- **✅ Safe**: Parameterized queries, proper disposal, and cancellation support

## 📦 Installation

```bash
# Main library
dotnet add package Tika.BatchIngestor

# Abstractions only (for library authors)
dotnet add package Tika.BatchIngestor.Abstractions
```

## 🎯 Quick Start

### Basic Usage

```csharp
using Tika.BatchIngestor;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Dialects;
using Tika.BatchIngestor.Factories;
using Microsoft.Data.SqlClient;

// Configure options with CPU throttling and performance monitoring
var options = new BatchIngestOptions
{
    BatchSize = 1000,
    MaxDegreeOfParallelism = 4,
    MaxInFlightBatches = 10,
    UseTransactions = true,
    TransactionPerBatch = true,

    // NEW: CPU Throttling (keeps CPU under 80%)
    EnableCpuThrottling = true,
    MaxCpuPercent = 80.0,
    ThrottleDelayMs = 100,

    // NEW: Performance Metrics
    EnablePerformanceMetrics = true,
    PerformanceMetricsIntervalMs = 1000,

    OnProgress = metrics =>
    {
        Console.WriteLine($"Progress: {metrics.TotalRowsProcessed:N0} rows, " +
                         $"{metrics.RowsPerSecond:N0} rows/sec, " +
                         $"CPU: {metrics.CurrentPerformance?.CpuUsagePercent:F2}%, " +
                         $"Memory: {metrics.CurrentPerformance?.WorkingSetMB:F2}MB");
    }
};

// Create connection factory
var connectionFactory = new SimpleConnectionFactory(
    "Server=localhost;Database=MyDb;...",
    () => new SqlConnection("Server=localhost;Database=MyDb;...")
);

// Create mapper
var mapper = new DefaultRowMapper<Customer>(
    c => new Dictionary<string, object?>
    {
        ["Id"] = c.Id,
        ["Name"] = c.Name,
        ["Email"] = c.Email
    }
);

// Create ingestor
var ingestor = new BatchIngestor<Customer>(
    connectionFactory,
    new SqlServerDialect(),
    mapper,
    options
);

// Ingest data
var data = GetCustomersAsync(); // IAsyncEnumerable<Customer>
var metrics = await ingestor.IngestAsync(data, "Customers");

// View comprehensive metrics
Console.WriteLine($"Ingested {metrics.TotalRowsProcessed:N0} rows in {metrics.ElapsedTime}");
Console.WriteLine($"Throughput: {metrics.RowsPerSecond:N0} rows/sec");
Console.WriteLine($"Peak CPU: {metrics.PeakPerformance?.CpuUsagePercent:F2}%");
Console.WriteLine($"Peak Memory: {metrics.PeakPerformance?.PeakWorkingSetMB:F2}MB");
```

### IoT / Time-Series Scenario

```csharp
// Optimized for IoT sensor data ingestion
var options = new BatchIngestOptions
{
    BatchSize = 5000,              // Large batches for small records
    MaxDegreeOfParallelism = 8,    // High parallelism for throughput
    MaxInFlightBatches = 20,       // More in-flight batches
    EnableCpuThrottling = true,
    MaxCpuPercent = 85.0           // Allow higher CPU for IoT workloads
};

var ingestor = new BatchIngestor<SensorReading>(
    connectionFactory,
    new AuroraPostgreSqlDialect(), // Cloud-optimized dialect
    mapper,
    options
);

// Achieves 10,000-15,000 rows/sec with controlled CPU
var metrics = await ingestor.IngestAsync(sensorData, "SensorReadings");
```

## 🔬 Performance Tuning

### Recommended Configurations

| Scenario | BatchSize | MaxDOP | MaxInFlight | MaxCPU% | Expected Throughput |
|----------|-----------|--------|-------------|---------|-------------------|
| **IoT Sensors** (small, ~100 bytes) | 5000-10000 | 6-8 | 15-20 | 80-85% | 10,000-15,000 rows/sec |
| **Vehicle Telemetry** (medium, ~500 bytes) | 2000-5000 | 4-6 | 10-15 | 75-80% | 7,000-12,000 rows/sec |
| **Time-Series Metrics** (minimal, ~64 bytes) | 8000-15000 | 8 | 20 | 85% | 12,000-18,000 rows/sec |
| **Industrial Logs** (large, ~2KB) | 500-1000 | 2-4 | 5-10 | 70-75% | 3,000-6,000 rows/sec |
| **Local Database** | 2000-5000 | 4-8 | 10-20 | 80% | 8,000-12,000 rows/sec |
| **Remote Database** | 1000-2000 | 2-4 | 5-10 | 75% | 5,000-8,000 rows/sec |
| **Cloud RDS** (Aurora/Azure) | 1500-3000 | 4-6 | 10-15 | 80% | 6,000-10,000 rows/sec |

### CPU & Memory Optimization

The library includes several performance optimizations:

- **Lock-Free Metrics**: Atomic operations with `Interlocked` eliminate lock contention
- **Zero-Allocation Patterns**: `ListSegment<T>` struct avoids intermediate allocations
- **Optimized Task.Yield**: Periodic yielding instead of per-item reduces context switching
- **CPU Throttling**: Automatic backoff when CPU exceeds threshold
- **Memory Monitoring**: Real-time GC and memory pressure tracking

## 🏥 Health Checks & Monitoring

### ASP.NET Core Health Checks Integration

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<IHealthCheckPublisher, BatchIngestorHealthCheckPublisher>();

services.AddHealthChecks()
    .AddBatchIngestorHealthCheck("batch-ingestor");

// Access health endpoint
// GET /health
// {
//   "status": "Healthy",
//   "results": {
//     "batch-ingestor": {
//       "status": "Healthy",
//       "description": "Operating normally. Throughput: 8,432 rows/sec, CPU: 45.23%",
//       "data": {
//         "TotalRowsProcessed": 1000000,
//         "BatchesCompleted": 1000,
//         "RowsPerSecond": 8432.5,
//         "CpuUsagePercent": 45.23,
//         "MemoryMB": 256.8
//       }
//     }
//   }
// }
```

### Manual Health Checks

```csharp
var performanceMetrics = new PerformanceMetrics();
var healthCheckPublisher = new BatchIngestorHealthCheckPublisher(
    metrics,
    performanceMetrics,
    options
);

var healthResult = healthCheckPublisher.GetHealthCheckResult();

if (healthResult.Status == HealthStatus.Unhealthy)
{
    Console.WriteLine($"System unhealthy: {healthResult.Description}");
    // Take action: scale resources, alert ops team, etc.
}
```

## 🌐 Cloud Database Support

### Amazon Aurora PostgreSQL

```csharp
using Npgsql;
using Tika.BatchIngestor.Dialects;

var connectionFactory = new SimpleConnectionFactory(
    "Host=mydb.cluster-xyz.us-east-1.rds.amazonaws.com;Database=mydb;...",
    () => new NpgsqlConnection("Host=...")
);

var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    new AuroraPostgreSqlDialect(), // Aurora-optimized
    mapper,
    options
);
```

### Amazon Aurora MySQL

```csharp
using MySqlConnector;
using Tika.BatchIngestor.Dialects;

var connectionFactory = new SimpleConnectionFactory(
    "Server=mydb.cluster-xyz.us-east-1.rds.amazonaws.com;Database=mydb;...",
    () => new MySqlConnection("Server=...")
);

var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    new AuroraMySqlDialect(), // Aurora MySQL-optimized
    mapper,
    options
);
```

### Azure SQL Database

```csharp
using Microsoft.Data.SqlClient;
using Tika.BatchIngestor.Dialects;

var connectionFactory = new SimpleConnectionFactory(
    "Server=tcp:myserver.database.windows.net,1433;Database=mydb;...",
    () => new SqlConnection("Server=...")
);

var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    new AzureSqlDialect(), // Azure SQL-optimized
    mapper,
    options
);
```

## 📊 Benchmarking

Run comprehensive benchmarks to validate performance on your hardware:

```bash
cd benchmarks/Tika.BatchIngestor.Benchmarks
dotnet run -c Release

# Benchmark scenarios:
# - Small IoT Sensor Readings (~100 bytes/row)
# - Medium Vehicle Telemetry (~500 bytes/row)
# - Minimal Time-Series Metrics (~64 bytes/row)
# - Large Industrial Machine Logs (~2KB/row)

# Test matrix:
# - Row Counts: 5,000 | 10,000 | 15,000
# - Batch Sizes: 500 | 1,000 | 2,000
# - Max Parallelism: 2 | 4 | 8
```

### Sample Benchmark Results

```
| Method                    | RowCount | BatchSize | MaxDOP | Mean      | StdDev    | Throughput   | CPU Peak | Memory Peak |
|---------------------------|----------|-----------|--------|-----------|-----------|--------------|----------|-------------|
| IngestSensorReadings      | 10000    | 1000      | 4      | 1.234 s   | 0.042 s   | 8,103 rows/s | 72.3%    | 145 MB      |
| IngestVehicleTelemetry    | 10000    | 1000      | 4      | 1.567 s   | 0.038 s   | 6,380 rows/s | 68.5%    | 198 MB      |
| IngestTimeSeriesMetrics   | 15000    | 2000      | 8      | 1.198 s   | 0.031 s   | 12,521 rows/s| 79.8%    | 132 MB      |
| IngestIndustrialLogs      | 5000     | 500       | 2      | 1.789 s   | 0.056 s   | 2,795 rows/s | 58.2%    | 312 MB      |
```

## 🔌 Supported Databases

The library works with any ADO.NET provider:

| Database | Dialect | Cloud Support | Max Params | Notes |
|----------|---------|---------------|------------|-------|
| **SQL Server** | `SqlServerDialect` | ✅ Azure SQL | 2,100 | Optimized for Azure |
| **PostgreSQL** | `GenericSqlDialect` | ✅ Aurora | 32,767 | High parameter limit |
| **MySQL** | `GenericSqlDialect` | ✅ Aurora | 32,767 | Aurora-optimized |
| **SQLite** | `GenericSqlDialect` | ❌ | 32,767 | Excellent for local/testing |
| **Oracle** | `GenericSqlDialect` | ✅ RDS | Custom | Use generic dialect |
| **MariaDB** | `GenericSqlDialect` | ✅ RDS | 32,767 | Compatible with MySQL |

## 🔧 Advanced Configuration

### Idempotent Inserts

#### Strategy 1: Staging Table + MERGE
```sql
-- Insert to staging table (always succeeds)
INSERT INTO Customers_Staging (...) VALUES ...

-- Then MERGE (idempotent)
MERGE INTO Customers AS target
USING Customers_Staging AS source
ON target.Id = source.Id
WHEN NOT MATCHED THEN INSERT ...
WHEN MATCHED THEN UPDATE ...
```

#### Strategy 2: INSERT IGNORE / ON CONFLICT
```sql
-- PostgreSQL
INSERT INTO Customers (...) VALUES (...)
ON CONFLICT (Id) DO NOTHING;

-- MySQL
INSERT IGNORE INTO Customers (...) VALUES (...);
```

### Custom SQL Dialect

```csharp
public class OracleDialect : ISqlDialect
{
    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";

    public string GetParameterName(int index) => $":p{index}";

    public int GetMaxParametersPerCommand() => 32767;

    public string BuildMultiRowInsert(string tableName, IReadOnlyList<string> columns, int rowCount)
    {
        // Oracle uses INSERT ALL syntax
        var sb = new StringBuilder("INSERT ALL ");
        for (int i = 0; i < rowCount; i++)
        {
            sb.Append($"INTO {tableName} (...) VALUES (...) ");
        }
        sb.Append("SELECT 1 FROM DUAL");
        return sb.ToString();
    }
}
```

### Custom Bulk Strategy

```csharp
// Example: SqlBulkCopy adapter
public class SqlBulkCopyStrategy<T> : IBulkInsertStrategy<T>
{
    public async Task<int> ExecuteAsync(
        DbConnection connection,
        string tableName,
        IReadOnlyList<string> columns,
        IReadOnlyList<T> rows,
        IRowMapper<T> mapper,
        CancellationToken cancellationToken)
    {
        using var bulkCopy = new SqlBulkCopy((SqlConnection)connection);
        bulkCopy.DestinationTableName = tableName;

        // Convert rows to DataTable
        var dataTable = ConvertToDataTable(rows, columns, mapper);

        await bulkCopy.WriteToServerAsync(dataTable, cancellationToken);
        return rows.Count;
    }
}

// Usage
var strategy = new SqlBulkCopyStrategy<Customer>();
var ingestor = new BatchIngestor<Customer>(
    connectionFactory,
    new SqlServerDialect(),
    mapper,
    options,
    strategy  // Custom strategy
);
```

## 🧪 Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run benchmarks
cd benchmarks/Tika.BatchIngestor.Benchmarks
dotnet run -c Release
```

## 📚 Documentation

- [Architecture Overview](docs/architecture.md)
- [Performance Tuning Guide](docs/performance-tuning.md)
- [Cloud Deployment Guide](docs/cloud-deployment.md)
- [Health Check Integration](docs/health-checks.md)
- [Troubleshooting](docs/troubleshooting.md)

## 🤝 Contributing

Contributions are welcome! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

### Development Prompts

See the [`prompts/`](prompts/) directory for AI-assisted development prompts to help contributors:
- Adding new SQL dialects
- Implementing custom bulk strategies
- Performance optimization techniques
- Testing and benchmarking guidelines

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

Built for high-scale IoT and telemetry ingestion scenarios, inspired by real-world production challenges in fleet management systems.

## 📈 Roadmap

- [x] Lock-free metrics collection
- [x] CPU throttling & monitoring
- [x] ASP.NET Core Health Checks integration
- [x] Cloud RDS dialect support (Aurora, Azure SQL)
- [x] Comprehensive benchmarking suite
- [ ] Distributed tracing support (OpenTelemetry)
- [ ] Bulk upsert strategies
- [ ] Connection pool monitoring
- [ ] Adaptive batch sizing based on performance

## 💬 Support

- 🐛 [Report a bug](https://github.com/TarekFawaz/Tika.BatchIngestor/issues)
- 💡 [Request a feature](https://github.com/TarekFawaz/Tika.BatchIngestor/issues)
- 📖 [Documentation](https://github.com/TarekFawaz/Tika.BatchIngestor/wiki)
- 💬 [Discussions](https://github.com/TarekFawaz/Tika.BatchIngestor/discussions)

---

**⭐ If you find this library useful, please consider giving it a star on GitHub!**
