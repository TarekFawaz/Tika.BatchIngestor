# Tika.BatchIngestor

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

// Define your data model
public class Customer
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public DateTime CreatedAt { get; set; }
}

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
        ["Email"] = c.Email,
        ["CreatedAt"] = c.CreatedAt
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
var customers = GenerateCustomers(); // IAsyncEnumerable<Customer>
var metrics = await ingestor.IngestAsync(
    customers,
    "Customers",
    cancellationToken
);

Console.WriteLine($"Ingested {metrics.TotalRowsProcessed:N0} rows in {metrics.ElapsedTime}");
```

### Dapper-Friendly Usage
```csharp
// Use the same connection approach with Dapper
var mapper = new DefaultRowMapper<dynamic>(
    record => new Dictionary<string, object?>
    {
        ["Id"] = record.Id,
        ["Name"] = record.Name,
        ["Email"] = record.Email
    }
);

var records = new[]
{
    new { Id = 1, Name = "Alice", Email = "alice@example.com" },
    new { Id = 2, Name = "Bob", Email = "bob@example.com" }
}.ToAsyncEnumerable();

await ingestor.IngestAsync(records, "Customers", cancellationToken);
```

## 🎛️ Configuration Guide

### Recommended Defaults

For most scenarios, these defaults work well:
```csharp
var options = new BatchIngestOptions
{
    BatchSize = 1000,                          // Sweet spot for most databases
    MaxDegreeOfParallelism = Environment.ProcessorCount / 2, // Conservative
    MaxInFlightBatches = 10,                   // Limits memory usage
    CommandTimeoutSeconds = 300,               // 5 minutes
    UseTransactions = true,                    // Consistency
    TransactionPerBatch = true,                // Better for large ingests
    RetryPolicy = new RetryPolicy 
    { 
        MaxRetries = 3, 
        InitialDelayMs = 100 
    }
};
```

### Performance Tuning Matrix

| Scenario | BatchSize | MaxDOP | MaxInFlight | Notes |
|----------|-----------|--------|-------------|-------|
| Small rows (<100 bytes) | 5000-10000 | 4-8 | 5-10 | Network bound |
| Large rows (>1KB) | 500-1000 | 2-4 | 5 | Memory bound |
| Local database | 2000-5000 | 4-8 | 10-20 | Low latency |
| Remote database | 1000-2000 | 2-4 | 5-10 | High latency |
| OLTP database (active) | 500-1000 | 2-3 | 5 | Avoid blocking |
| Dedicated warehouse | 5000-10000 | 8-16 | 20 | Max throughput |

## 🔬 Deep Dive: Batch Size vs Parallelism vs Constraints

### Understanding the Tradeoffs

#### Batch Size
- **Larger batches** (5000-10000):
  - ✅ Fewer round trips to database
  - ✅ Better throughput for network-bound scenarios
  - ❌ More memory per batch
  - ❌ Longer locks held in database
  - ❌ Larger transaction logs
  - ❌ May hit parameter limits (SQL Server: 2100 params)

- **Smaller batches** (500-1000):
  - ✅ Less memory pressure
  - ✅ Shorter lock duration
  - ✅ Better for concurrent OLTP workloads
  - ❌ More round trips
  - ❌ Lower raw throughput

#### Max Degree of Parallelism (MaxDOP)
- **Higher parallelism** (8-16):
  - ✅ Maximum throughput on high-latency connections
  - ✅ Better CPU utilization
  - ❌ More concurrent connections to database
  - ❌ Can overwhelm database with lock contention
  - ❌ Higher memory usage (more batches in flight)

- **Lower parallelism** (2-4):
  - ✅ Less database connection pressure
  - ✅ Safer for shared OLTP databases
  - ✅ More predictable resource usage
  - ❌ Lower throughput potential

#### MaxInFlightBatches (Backpressure Control)
This is your **primary memory control** mechanism. It limits how many batches can be buffered between the producer and consumers.
```
Formula: Max Memory ≈ MaxInFlightBatches × BatchSize × AvgRowSize
```

Example: 10 batches × 1000 rows × 500 bytes = ~5 MB

**This library does NOT have a MaxMemoryMegabytes setting** because:
1. Accurate memory tracking is expensive and unreliable in .NET
2. MaxInFlightBatches provides indirect but effective memory control
3. The bounded channel pattern naturally limits memory growth

### Network/IO Bound vs CPU Bound

**Most database insert scenarios are IO/Network bound**, not CPU bound:

- CPU usage is typically **low** during batch inserts (10-30%)
- Bottleneck is usually:
  - Network latency to database
  - Disk IO on database server
  - Database lock contention
  - Transaction log flush times

**Therefore, MaxCpuPercent is NOT implemented** because:
1. It's not the real bottleneck in 95% of cases
2. .NET doesn't provide reliable in-process CPU throttling
3. CPU usage is already controlled indirectly by:
   - `MaxDegreeOfParallelism` (limits concurrent work)
   - `BatchSize` (limits work per operation)
   - Database response times (natural throttling)

**If you need CPU throttling**, tune these instead:
- Lower `MaxDegreeOfParallelism` (fewer concurrent operations)
- Lower `BatchSize` (less work per operation)
- Use `Task.Delay()` in callbacks for artificial throttling

### Database-Specific Limits

#### SQL Server
- **Parameter limit**: 2100 parameters per query
  - Each row with N columns = N parameters
  - Max rows per batch ≈ 2100 / column_count
  - Example: 10 columns → max 210 rows per INSERT
- **Transaction log**: Large batches can cause log growth
  - Use `TransactionPerBatch = true` for better log management
  - Consider smaller batches if log is constrained
- **Lock escalation**: 5000+ row modifications may escalate to table lock
  - Use smaller batches for active OLTP tables

#### PostgreSQL
- **No hard parameter limit**, but performance degrades >10000 params
- **COPY command**: For maximum speed, implement custom `IBulkInsertStrategy`
- **Connection pooling**: Tune pool size based on MaxDOP

#### MySQL
- **max_allowed_packet**: Limits total SQL statement size (default 4MB)
  - Calculate: batch_size × row_size × safety_factor < max_allowed_packet
- **innodb_buffer_pool_size**: Affects concurrent insert performance

### Transactions: Per-Batch vs Single
```csharp
// Option 1: Transaction per batch (RECOMMENDED for large ingests)
TransactionPerBatch = true
```
- ✅ Better for long-running ingests (millions of rows)
- ✅ Limits transaction log growth
- ✅ Allows partial success (earlier batches committed)
- ❌ Not atomic across entire dataset
```csharp
// Option 2: Single transaction (all-or-nothing)
UseTransactions = true, TransactionPerBatch = false
```
- ✅ Atomic: all rows or none
- ✅ Simpler rollback semantics
- ❌ Large transaction logs
- ❌ Long-held locks
- ❌ Not practical for millions of rows

### Failure Modes and Retries

#### Retry Policy Configuration
```csharp
RetryPolicy = new RetryPolicy
{
    MaxRetries = 3,              // How many times to retry
    InitialDelayMs = 100,        // First retry after 100ms
    MaxDelayMs = 5000,           // Cap exponential backoff at 5s
    UseExponentialBackoff = true, // 100ms, 200ms, 400ms, 800ms...
    UseJitter = true             // Add randomness to avoid thundering herd
}
```

#### Transient vs Permanent Failures

**Retriable (transient) errors**:
- Network timeouts
- Database deadlocks (SQL Server error 1205)
- Connection pool exhaustion
- Temporary resource unavailability

**Non-retriable (permanent) errors**:
- Constraint violations (duplicate keys, foreign keys)
- Data type mismatches
- Permission denied
- Schema doesn't exist

The library retries transient errors automatically. Permanent errors fail immediately.

#### Error Handling Patterns
```csharp
try
{
    var metrics = await ingestor.IngestAsync(data, "Table", ct);
}
catch (BatchIngestException ex)
{
    // Contains details about which batch failed
    Console.WriteLine($"Failed at batch {ex.BatchNumber}");
    Console.WriteLine($"Rows processed before failure: {ex.RowsProcessedBeforeFailure}");
    
    // Inner exception contains database error
    if (ex.InnerException is SqlException sqlEx)
    {
        // Handle SQL-specific errors
    }
}
```

### Idempotency Considerations

**The library does NOT provide automatic idempotency**. You must handle this at the application level:

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
    
    public int GetMaxParametersPerCommand() => 32767;
    
    public string BuildMultiRowInsert(
        string tableName, 
        IReadOnlyList<string> columns, 
        int rowCount)
    {
        var quotedTable = QuoteIdentifier(tableName);
        var quotedColumns = string.Join(", ", columns.Select(QuoteIdentifier));
        
        var valueRows = new List<string>();
        for (int row = 0; row < rowCount; row++)
        {
            var values = columns.Select((_, colIdx) => 
                GetParameterName(row * columns.Count + colIdx));
            valueRows.Add($"({string.Join(", ", values)})");
        }
        
        return $"INSERT INTO {quotedTable} ({quotedColumns}) VALUES {string.Join(", ", valueRows)}";
    }
}
```

### Custom Bulk Strategy (Provider-Specific)
```csharp
// Example: SqlBulkCopy adapter (in a separate package)
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
        using var bulkCopy = new SqlBulkCopy((SqlConnection)connection)
        {
            DestinationTableName = tableName,
            BatchSize = rows.Count
        };
        
        // Map columns
        foreach (var column in columns)
            bulkCopy.ColumnMappings.Add(column, column);
        
        // Create DataTable
        var dataTable = new DataTable();
        foreach (var column in columns)
            dataTable.Columns.Add(column);
            
        foreach (var row in rows)
        {
            var mappedRow = mapper.Map(row);
            var dataRow = dataTable.NewRow();
            foreach (var kvp in mappedRow)
                dataRow[kvp.Key] = kvp.Value ?? DBNull.Value;
            dataTable.Rows.Add(dataRow);
        }
        
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

## 📊 Measuring Performance

### Benchmark Template
```csharp
using System.Diagnostics;
using BenchmarkDotNet.Attributes;

[MemoryDiagnoser]
public class BatchIngestBenchmarks
{
    private List<Customer> _data = null!;
    
    [Params(500, 1000, 5000)]
    public int BatchSize { get; set; }
    
    [Params(2, 4, 8)]
    public int MaxDOP { get; set; }
    
    [GlobalSetup]
    public void Setup()
    {
        _data = GenerateCustomers(100_000).ToList();
    }
    
    [Benchmark]
    public async Task IngestWithOptions()
    {
        var options = new BatchIngestOptions
        {
            BatchSize = BatchSize,
            MaxDegreeOfParallelism = MaxDOP
        };
        
        // Benchmark code here
    }
}
```

### Key Metrics to Track

1. **Throughput**: Rows per second
2. **Latency**: Time to first row, time to completion
3. **Resource Usage**: Peak memory, CPU percentage
4. **Database Impact**: Lock duration, transaction log size, IO wait times
5. **Error Rate**: Failed batches, retry count

### Real-World Testing Checklist

- [ ] Test with realistic data volume (millions of rows)
- [ ] Test with realistic row size
- [ ] Test concurrent ingests
- [ ] Test on same network topology as production (local vs remote DB)
- [ ] Test with production database load
- [ ] Monitor database metrics (CPU, IO, locks, waits)
- [ ] Test failure scenarios (kill connections, deadlocks)
- [ ] Test memory growth over long runs
- [ ] Test cancellation responsiveness

## 🗺️ Roadmap

### Planned Features
- [ ] Built-in SqlBulkCopy adapter (separate package)
- [ ] PostgreSQL COPY adapter
- [ ] MySQL LOAD DATA adapter
- [ ] Circuit breaker pattern integration
- [ ] Metrics export (Prometheus, OpenTelemetry)
- [ ] Dead letter queue for failed rows
- [ ] Schema inference from POCO
- [ ] Distributed tracing support
- [ ] Async batch streaming (IAsyncEnumerable all the way)

### Contributions Welcome
This is an open-source project. Contributions, issues, and feature requests are welcome!

## 📄 License

MIT License - see LICENSE file for details.

## 🙏 Acknowledgments

Built for high-scale IoT and telemetry ingestion scenarios, inspired by real-world production challenges in fleet management systems.
