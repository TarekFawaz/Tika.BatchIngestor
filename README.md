> **Important Note**
> This entire concept is created for educational purposes for beginners; however, it is designed to be *high-performance and production-ready*.

# Tika.BatchIngestor

[![NuGet](https://img.shields.io/nuget/v/Tika.BatchIngestor.svg)](https://www.nuget.org/packages/Tika.BatchIngestor/)
[![Build](https://github.com/TarekFawaz/Tika.BatchIngestor/workflows/CI/badge.svg)](https://github.com/TarekFawaz/Tika.BatchIngestor/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A **high-performance, production-ready** .NET library for efficiently ingesting large volumes of data into relational databases with **optimized CPU usage**, **memory management**, and **real-time health monitoring**.

## What is Tika.BatchIngestor?

Tika.BatchIngestor is a lightweight, RDBMS-agnostic library designed to solve the common problem of bulk data insertion into relational databases. It provides:

- **High Throughput**: 5,000-15,000+ rows/sec with optimized batch processing
- **CPU Control**: Built-in throttling to keep CPU usage under configurable limits (default: 80%)
- **Memory Optimized**: Lock-free atomic operations, zero-allocation patterns, minimal GC pressure
- **Real-Time Metrics**: CPU, memory, throughput, and performance monitoring
- **Health Checks**: ASP.NET Core Health Checks integration for production monitoring
- **Cloud-Ready**: Optimized dialects for Aurora PostgreSQL, Aurora MySQL, and Azure SQL
- **Resource Control**: Memory and concurrency limits via bounded channels
- **RDBMS Agnostic**: Works with any ADO.NET provider (SQL Server, PostgreSQL, MySQL, SQLite, etc.)
- **Extensible**: Plugin architecture for custom dialects and bulk strategies
- **Observable**: Built-in metrics, progress callbacks, and structured logging
- **Resilient**: Retry policies with exponential backoff and jitter
- **Safe**: Parameterized queries, proper disposal, and cancellation support

## Installation

```bash
# Main library
dotnet add package Tika.BatchIngestor

# Abstractions only (for library authors)
dotnet add package Tika.BatchIngestor.Abstractions

# DI Extensions (for ASP.NET Core / DI integration)
dotnet add package Tika.BatchIngestor.Extensions.DependencyInjection
```

## Quick Start

### Option 1: Direct Code Usage

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

    // CPU Throttling (keeps CPU under 80%)
    EnableCpuThrottling = true,
    MaxCpuPercent = 80.0,
    ThrottleDelayMs = 100,

    // Performance Metrics
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
    () => new SqlConnection()
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

### Option 2: Using Dependency Injection Extensions

```csharp
using Tika.BatchIngestor.Extensions.DependencyInjection;

// In Program.cs or Startup.cs
builder.Services.AddBatchIngestorFactory(builder.Configuration);

// Then in your service/controller:
public class MyService
{
    private readonly IBatchIngestorFactory _factory;

    public MyService(IBatchIngestorFactory factory)
    {
        _factory = factory;
    }

    public async Task IngestDataAsync(IEnumerable<Customer> customers, string connectionString)
    {
        var mapper = new DefaultRowMapper<Customer>(c => new Dictionary<string, object?>
        {
            ["Id"] = c.Id,
            ["Name"] = c.Name,
            ["Email"] = c.Email
        });

        // Create ingestor using dialect type string (configuration-driven)
        var ingestor = _factory.CreateIngestor("SqlServer", connectionString, mapper);

        // Or use strongly-typed methods
        var sqlServerIngestor = _factory.CreateSqlServerIngestor(connectionString, mapper);
        var postgresIngestor = _factory.CreatePostgreSqlIngestor(connectionString, mapper);
        var auroraPostgresIngestor = _factory.CreateAuroraPostgreSqlIngestor(connectionString, mapper);
        var auroraMySqlIngestor = _factory.CreateAuroraMySqlIngestor(connectionString, mapper);
        var azureSqlIngestor = _factory.CreateAzureSqlIngestor(connectionString, mapper);

        await ingestor.IngestAsync(customers, "Customers");
    }
}
```

## Configuration via appsettings.json

Configure multiple database connections and default settings in your `appsettings.json`:

```json
{
  "BatchIngestor": {
    "DefaultBatchSize": 1000,
    "DefaultMaxDegreeOfParallelism": 4,
    "EnableCpuThrottling": true,
    "MaxCpuPercent": 80.0,
    "ThrottleDelayMs": 100,
    "EnablePerformanceMetrics": true,
    "RetryPolicy": {
      "MaxRetries": 3,
      "InitialDelayMs": 100,
      "MaxDelayMs": 5000,
      "UseExponentialBackoff": true,
      "UseJitter": true
    },
    "Connections": [
      {
        "Name": "SqlServer",
        "Dialect": "SqlServer",
        "ConnectionString": "Server=localhost;Database=MyDb;Trusted_Connection=True;",
        "Enabled": true
      },
      {
        "Name": "PostgreSql",
        "Dialect": "PostgreSql",
        "ConnectionString": "Host=localhost;Database=mydb;Username=postgres;Password=secret;",
        "Enabled": true
      },
      {
        "Name": "AuroraPostgreSql",
        "Dialect": "AuroraPostgreSql",
        "ConnectionString": "Host=mydb.cluster-xyz.us-east-1.rds.amazonaws.com;Database=mydb;...",
        "Enabled": true
      },
      {
        "Name": "AuroraMySql",
        "Dialect": "AuroraMySql",
        "ConnectionString": "Server=mydb.cluster-xyz.us-east-1.rds.amazonaws.com;Database=mydb;...",
        "Enabled": false
      },
      {
        "Name": "AzureSql",
        "Dialect": "AzureSql",
        "ConnectionString": "Server=tcp:myserver.database.windows.net,1433;Database=mydb;...",
        "Enabled": true
      }
    ]
  }
}
```

### Supported Dialect Types

| Dialect | String Value | ADO.NET Provider | Description |
|---------|--------------|------------------|-------------|
| SQL Server | `SqlServer` | `Microsoft.Data.SqlClient` | On-premises SQL Server |
| PostgreSQL | `PostgreSql` | `Npgsql` | Standard PostgreSQL |
| Aurora PostgreSQL | `AuroraPostgreSql` | `Npgsql` | Amazon Aurora PostgreSQL |
| Aurora MySQL | `AuroraMySql` | `MySqlConnector` | Amazon Aurora MySQL |
| Azure SQL | `AzureSql` | `Microsoft.Data.SqlClient` | Azure SQL Database |
| Generic | `Generic` | Any ADO.NET | Default/fallback dialect |

### Using Settings in Code

```csharp
// Option 1: Load from IConfiguration
builder.Services.AddBatchIngestorFactory(builder.Configuration);

// Option 2: Configure programmatically
builder.Services.AddBatchIngestorFactory(settings =>
{
    settings.DefaultBatchSize = 2000;
    settings.EnableCpuThrottling = true;
    settings.MaxCpuPercent = 75.0;
    settings.Connections.Add(new DatabaseConnection
    {
        Name = "Primary",
        Dialect = "SqlServer",
        ConnectionString = "Server=...;Database=...;",
        Enabled = true
    });
});

// Option 3: Pass BatchIngestorSettings directly
var settings = new BatchIngestorSettings
{
    DefaultBatchSize = 1000,
    Connections = new List<DatabaseConnection>
    {
        new() { Name = "Main", Dialect = "PostgreSql", ConnectionString = "..." }
    }
};
builder.Services.AddBatchIngestorFactory(settings);
```

## Demo API

The repository includes a fully functional Demo API that showcases all batch ingestion capabilities with Swagger documentation.

### Running the Demo API

```bash
cd src/Tika.BatchIngestor.DemoApi
dotnet run
```

Then open `https://localhost:5001` for Swagger UI.

### Demo API Endpoints

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET | `/api/ingest/connections` | List all configured connections |
| GET | `/api/ingest/dialects` | List all supported dialect types |
| POST | `/api/ingest/{connectionName}/sensors` | Ingest sensors using named connection |
| POST | `/api/ingest/{connectionName}/customers` | Ingest customers using named connection |
| POST | `/api/ingest/{connectionName}/orders` | Ingest orders using named connection |
| POST | `/api/ingest/{connectionName}/sensors/generate` | Generate & ingest sample data |
| POST | `/api/ingest/direct/sensors` | Ingest with inline dialect/connection |
| GET | `/api/health` | Health check status |

### Sample Requests

```bash
# List configured connections
curl https://localhost:5001/api/ingest/connections

# Generate and ingest 10,000 sensor readings to SQL Server
curl -X POST "https://localhost:5001/api/ingest/SqlServer/sensors/generate?count=10000&tableName=SensorReadings"

# Ingest custom data
curl -X POST https://localhost:5001/api/ingest/PostgreSql/customers \
  -H "Content-Type: application/json" \
  -d '{
    "tableName": "customers",
    "data": [
      {"id": 1, "name": "John Doe", "email": "john@example.com", "city": "NYC", "createdAt": "2024-01-01T00:00:00Z"}
    ]
  }'
```

## Performance Tuning

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

## Health Checks & Monitoring

### ASP.NET Core Health Checks Integration

```csharp
// In Startup.cs or Program.cs
services.AddSingleton<IBatchIngestorHealthCheckPublisher, BatchIngestorHealthCheckPublisher>();

services.AddHealthChecks()
    .AddBatchIngestorHealthCheck("batch-ingestor");

// Access health endpoint: GET /health
```

## Cloud Database Support

### Using DI Factory (Recommended)

```csharp
// Amazon Aurora PostgreSQL
var ingestor = _factory.CreateAuroraPostgreSqlIngestor(connectionString, mapper);

// Amazon Aurora MySQL (requires MySqlConnector package)
var ingestor = _factory.CreateAuroraMySqlIngestor(connectionString, mapper);

// Azure SQL Database
var ingestor = _factory.CreateAzureSqlIngestor(connectionString, mapper);
```

### Direct Instantiation

```csharp
// Amazon Aurora PostgreSQL
var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    new AuroraPostgreSqlDialect(),
    mapper,
    options
);

// Azure SQL Database
var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    new AzureSqlDialect(),
    mapper,
    options
);
```

## Supported Databases

| Database | Dialect | Cloud Support | Max Params | Notes |
|----------|---------|---------------|------------|-------|
| **SQL Server** | `SqlServerDialect` | Azure SQL | 2,100 | Optimized for Azure |
| **PostgreSQL** | `GenericSqlDialect` | Aurora | 32,767 | High parameter limit |
| **MySQL** | `AuroraMySqlDialect` | Aurora | 32,767 | Aurora-optimized |
| **SQLite** | `GenericSqlDialect` | N/A | 32,767 | Excellent for local/testing |
| **Oracle** | `GenericSqlDialect` | RDS | Custom | Use generic dialect |
| **MariaDB** | `GenericSqlDialect` | RDS | 32,767 | Compatible with MySQL |

## Advanced Configuration

### Custom SQL Dialect

```csharp
public class OracleDialect : ISqlDialect
{
    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";
    public string GetParameterName(int index) => $":p{index}";
    public int GetMaxParametersPerCommand() => 32767;

    public string BuildMultiRowInsert(string tableName, IReadOnlyList<string> columns, int rowCount)
    {
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

## Testing

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"

# Run benchmarks
cd benchmarks/Tika.BatchIngestor.Benchmarks
dotnet run -c Release
```

## Documentation

- [Architecture Overview](docs/architecture.md)
- [Performance Tuning Guide](docs/performance-tuning.md)
- [Cloud Deployment Guide](docs/cloud-deployment.md)
- [Health Check Integration](docs/health-checks.md)
- [Troubleshooting](docs/troubleshooting.md)

## Contributing

Contributions are welcome! Please see our [Contributing Guide](CONTRIBUTING.md) for details.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## Roadmap

- [x] Lock-free metrics collection
- [x] CPU throttling & monitoring
- [x] ASP.NET Core Health Checks integration
- [x] Cloud RDS dialect support (Aurora, Azure SQL)
- [x] Comprehensive benchmarking suite
- [x] DI Extensions with appsettings configuration
- [x] Demo API with Swagger
- [ ] Distributed tracing support (OpenTelemetry)
- [ ] Bulk upsert strategies
- [ ] Connection pool monitoring
- [ ] Adaptive batch sizing based on performance

## Support

- [Report a bug](https://github.com/TarekFawaz/Tika.BatchIngestor/issues)
- [Request a feature](https://github.com/TarekFawaz/Tika.BatchIngestor/issues)
- [Documentation](https://github.com/TarekFawaz/Tika.BatchIngestor/wiki)
- [Discussions](https://github.com/TarekFawaz/Tika.BatchIngestor/discussions)

---

**If you find this library useful, please consider giving it a star on GitHub!**
