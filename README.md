# Tika.BatchIngestor

[![NuGet](https://img.shields.io/nuget/v/Tika.BatchIngestor.svg)](https://www.nuget.org/packages/Tika.BatchIngestor/)
[![Build](https://github.com/TarekFawaz/Tika.BatchIngestor/workflows/CI/badge.svg)](https://github.com/TarekFawaz/Tika.BatchIngestor/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)

A high-performance, production-ready .NET library for efficiently ingesting large volumes of data into relational databases with controlled parallelism, backpressure, and resource limits.

## 🚀 What is Tika.BatchIngestor?

Tika.BatchIngestor is a lightweight, RDBMS-agnostic library designed to solve the common problem of bulk data insertion into relational databases. It provides:

- **High Throughput**: Batch inserts with configurable parallelism
- **Resource Control**: Memory and concurrency limits via bounded channels
- **RDBMS Agnostic**: Works with any ADO.NET provider (SQL Server, PostgreSQL, MySQL, SQLite, etc.)
- **Extensible**: Plugin architecture for custom dialects and bulk strategies
- **Observable**: Built-in metrics, progress callbacks, and logging
- **Resilient**: Retry policies with exponential backoff and jitter
- **Safe**: Parameterized queries, proper disposal, and cancellation support

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
using Microsoft.Data.SqlClient;

// Configure options
var options = new BatchIngestOptions
{
    BatchSize = 1000,
    MaxDegreeOfParallelism = 4,
    MaxInFlightBatches = 10
};

// Create connection factory
var connectionFactory = new SimpleConnectionFactory(
    "Server=localhost;Database=MyDb;...",
    () => new SqlConnection()
);

// Configure options
var options = new BatchIngestOptions
{
    BatchSize = 1000,
    MaxDegreeOfParallelism = 4,
    MaxInFlightBatches = 10,
    UseTransactions = true,
    TransactionPerBatch = true,
    OnProgress = metrics => 
    {
        Console.WriteLine($"Progress: {metrics.TotalRowsProcessed:N0} rows, " +
                         $"{metrics.RowsPerSecond:N0} rows/sec");
    }
};

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

Console.WriteLine($"Ingested {metrics.TotalRowsProcessed:N0} rows in {metrics.ElapsedTime}");
```

## 📚 Documentation

For detailed documentation, see:
- [Design Document](docs/design.md) - Architecture and design decisions
- [API Reference](https://github.com/TarekFawaz/Tika.BatchIngestor/wiki) - Full API documentation

## 🎛️ Configuration

## 🎛️ Configuration Guide

### Recommended Defaults

For most scenarios, these defaults work well:
```csharp
var options = new BatchIngestOptions
{
    BatchSize = 1000,                    // Sweet spot for most databases
    MaxDegreeOfParallelism = 4,          // Conservative parallelism
    MaxInFlightBatches = 10,             // Memory control
    CommandTimeoutSeconds = 300,         // 5 minutes
    UseTransactions = true,
    TransactionPerBatch = true,          // Better for large ingests
    RetryPolicy = new RetryPolicy 
    { 
        MaxRetries = 3, 
        InitialDelayMs = 100 
    }
};
```

## 🔬 Performance Tuning

| Scenario | BatchSize | MaxDOP | MaxInFlight |
|----------|-----------|--------|-------------|
| Small rows (<100 bytes) | 5000-10000 | 4-8 | 5-10 |
| Large rows (>1KB) | 500-1000 | 2-4 | 5 |
| Local database | 2000-5000 | 4-8 | 10-20 |
| Remote database | 1000-2000 | 2-4 | 5-10 |

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

#### Strategy 3: Application-Level Deduplication
```csharp
// Check existing IDs first
var existingIds = await connection.QueryAsync<int>(
    "SELECT Id FROM Customers WHERE Id IN @Ids",
    new { Ids = batch.Select(c => c.Id) }
);

// Filter out existing
var newRecords = batch.Where(c => !existingIds.Contains(c.Id));
```

## 🔌 Extensibility

### Custom SQL Dialect

```csharp
public class PostgreSqlDialect : ISqlDialect
{
    public string QuoteIdentifier(string identifier) => $"\"{identifier}\"";
    
    public string GetParameterName(int index) => $"${index + 1}";
    // ... implement other methods
}
        
        return $"INSERT INTO {quotedTable} ({quotedColumns}) VALUES {string.Join(", ", valueRows)}";
    }
}
```

### Custom Bulk Strategy

```csharp
// Example: SqlBulkCopy adapter (in a separate package)
public class SqlBulkCopyStrategy<T> : IBulkInsertStrategy<T>
{
    public async Task<int> ExecuteAsync(/* ... */)
    {
        using var bulkCopy = new SqlBulkCopy((SqlConnection)connection);
        // ... configure and execute
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
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request.

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

Built for high-scale IoT and telemetry ingestion scenarios, inspired by real-world production challenges in fleet management systems.
