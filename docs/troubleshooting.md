# Troubleshooting Guide

This guide helps you diagnose and resolve common issues when using Tika.BatchIngestor.

## Table of Contents

- [Diagnostic Tools](#diagnostic-tools)
- [Common Issues](#common-issues)
- [Performance Issues](#performance-issues)
- [Error Scenarios](#error-scenarios)
- [Connection Issues](#connection-issues)
- [Memory Issues](#memory-issues)
- [Configuration Problems](#configuration-problems)
- [Database-Specific Issues](#database-specific-issues)
- [Getting Help](#getting-help)

## Diagnostic Tools

### Enable Detailed Logging

```csharp
using Microsoft.Extensions.Logging;

var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .AddDebug()
        .SetMinimumLevel(LogLevel.Debug);  // Or LogLevel.Trace for maximum detail
});

var options = new BatchIngestOptions
{
    // ... other config ...
    Logger = loggerFactory.CreateLogger<BatchIngestor<MyData>>()
};
```

### Monitor Metrics in Real-Time

```csharp
var options = new BatchIngestOptions
{
    EnablePerformanceMetrics = true,
    PerformanceMetricsIntervalMs = 1000,

    OnProgress = metrics =>
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] Progress Report:");
        Console.WriteLine($"  Rows Processed: {metrics.TotalRowsProcessed:N0}");
        Console.WriteLine($"  Batches Completed: {metrics.BatchesCompleted}");
        Console.WriteLine($"  Errors: {metrics.ErrorCount}");
        Console.WriteLine($"  Throughput: {metrics.RowsPerSecond:N0} rows/sec");
        Console.WriteLine($"  Avg Batch Duration: {metrics.AverageBatchDuration.TotalMilliseconds:N2}ms");

        if (metrics.CurrentPerformance != null)
        {
            var perf = metrics.CurrentPerformance;
            Console.WriteLine($"  CPU: {perf.CpuUsagePercent:F2}%");
            Console.WriteLine($"  Memory: {perf.WorkingSetMB:F2} MB");
            Console.WriteLine($"  Threads: {perf.ThreadCount}");
            Console.WriteLine($"  GC Gen0/Gen1/Gen2: {perf.Gen0Collections}/{perf.Gen1Collections}/{perf.Gen2Collections}");
        }
        Console.WriteLine();
    },

    OnBatchCompleted = (batchNum, duration) =>
    {
        Console.WriteLine($"[BATCH {batchNum}] Completed in {duration.TotalMilliseconds:N2}ms");
    }
};
```

### Capture Exception Details

```csharp
try
{
    var metrics = await ingestor.IngestAsync(data, "MyTable");
}
catch (BatchIngestException ex)
{
    Console.WriteLine($"Batch Ingest Failed:");
    Console.WriteLine($"  Batch Number: {ex.BatchNumber}");
    Console.WriteLine($"  Rows Processed Before Error: {ex.RowsProcessedBeforeError}");
    Console.WriteLine($"  Message: {ex.Message}");
    Console.WriteLine($"  Inner Exception: {ex.InnerException?.Message}");
    Console.WriteLine($"  Stack Trace: {ex.StackTrace}");
}
catch (Exception ex)
{
    Console.WriteLine($"Unexpected Error: {ex}");
}
```

### Diagnostic Configuration

```csharp
// Minimal overhead configuration for debugging
var diagnosticOptions = new BatchIngestOptions
{
    BatchSize = 100,               // Small batches for easier debugging
    MaxDegreeOfParallelism = 1,    // Single-threaded for deterministic behavior
    MaxInFlightBatches = 1,        // No buffering
    EnableCpuThrottling = false,   // No throttling delays
    EnablePerformanceMetrics = true,
    Logger = logger,

    OnProgress = metrics => { /* Log everything */ },
    OnBatchCompleted = (num, duration) => { /* Log each batch */ }
};
```

## Common Issues

### Issue: Low Throughput (<1000 rows/sec)

**Symptoms:**
- Ingestion is much slower than expected
- `RowsPerSecond` metric is low
- High batch duration

**Possible Causes:**

1. **Batch size too small**
   ```csharp
   // ❌ BAD: Too small
   BatchSize = 10

   // ✅ GOOD: Appropriate size
   BatchSize = 2000  // Start here and adjust
   ```

2. **Not enough parallelism**
   ```csharp
   // ❌ BAD: Single-threaded
   MaxDegreeOfParallelism = 1

   // ✅ GOOD: Use multiple cores
   MaxDegreeOfParallelism = Environment.ProcessorCount / 2
   ```

3. **CPU throttling too aggressive**
   ```csharp
   // ❌ BAD: Too conservative
   MaxCpuPercent = 30.0

   // ✅ GOOD: Allow more CPU
   MaxCpuPercent = 80.0
   // Or disable:
   EnableCpuThrottling = false
   ```

4. **Database is the bottleneck**
   - Check database CPU and IO metrics
   - Verify network latency to database
   - Check for database locks or contention

**Diagnosis:**
```csharp
var options = new BatchIngestOptions
{
    EnablePerformanceMetrics = true,
    OnProgress = metrics =>
    {
        var cpu = metrics.CurrentPerformance?.CpuUsagePercent ?? 0;

        if (cpu < 40)
        {
            Console.WriteLine("⚠️ Low CPU usage - increase BatchSize or MaxDOP");
        }

        if (metrics.AverageBatchDuration.TotalSeconds > 1)
        {
            Console.WriteLine("⚠️ Slow batch processing - check database performance");
        }
    }
};
```

**Solutions:**
1. Increase `BatchSize` to 2000-5000
2. Increase `MaxDegreeOfParallelism` to 4-8
3. Adjust or disable CPU throttling
4. Optimize database (indexes, resources, etc.)

### Issue: Out of Memory (OOM)

**Symptoms:**
- `OutOfMemoryException` thrown
- Process crashes
- High GC Gen2 collections

**Possible Causes:**

1. **Too many in-flight batches**
   ```csharp
   // ❌ BAD: Too much buffering
   MaxInFlightBatches = 100

   // ✅ GOOD: Reasonable limit
   MaxInFlightBatches = 10
   ```

2. **Batch size too large**
   ```csharp
   // ❌ BAD: Huge batches
   BatchSize = 100000

   // ✅ GOOD: Manageable size
   BatchSize = 2000
   ```

3. **Large row size**
   - Calculate: `MaxInFlightBatches × BatchSize × AvgRowSize`
   - Example: `20 × 5000 × 1KB = ~100 MB`

**Diagnosis:**
```csharp
var options = new BatchIngestOptions
{
    EnablePerformanceMetrics = true,
    OnProgress = metrics =>
    {
        var perf = metrics.CurrentPerformance;
        if (perf != null)
        {
            Console.WriteLine($"Memory: {perf.WorkingSetMB:F2} MB (Peak: {perf.PeakWorkingSetMB:F2} MB)");
            Console.WriteLine($"GC Collections: Gen0={perf.Gen0Collections}, Gen1={perf.Gen1Collections}, Gen2={perf.Gen2Collections}");

            if (perf.Gen2Collections > 10)
            {
                Console.WriteLine("⚠️ Frequent Gen2 GC - reduce memory pressure");
            }
        }
    }
};
```

**Solutions:**
1. Reduce `MaxInFlightBatches` to 5-10
2. Reduce `BatchSize` to 1000-2000
3. Process data in smaller chunks
4. Disable `EnablePerformanceMetrics` if not needed

### Issue: High Error Rate

**Symptoms:**
- Many batches failing
- `ErrorCount` in metrics is high
- Exceptions in logs

**Possible Causes:**

1. **Database constraints violated**
   - Primary key conflicts
   - Foreign key violations
   - Check constraints
   - NOT NULL violations

2. **Connection issues**
   - Transient network errors
   - Connection pool exhausted
   - Database timeouts

3. **Data issues**
   - Invalid data types
   - Data too large for columns
   - Encoding issues

**Diagnosis:**
```csharp
try
{
    var metrics = await ingestor.IngestAsync(data, "MyTable");

    if (metrics.ErrorCount > 0)
    {
        var errorRate = (double)metrics.ErrorCount / metrics.BatchesCompleted;
        Console.WriteLine($"Error Rate: {errorRate:P2}");
        Console.WriteLine($"Total Errors: {metrics.ErrorCount}");
        Console.WriteLine($"Batches Completed: {metrics.BatchesCompleted}");
    }
}
catch (BatchIngestException ex)
{
    Console.WriteLine($"Failed at batch {ex.BatchNumber}");
    Console.WriteLine($"Inner exception: {ex.InnerException?.GetType().Name}");
    Console.WriteLine($"Message: {ex.InnerException?.Message}");

    // Check for specific error types
    if (ex.InnerException is SqlException sqlEx)
    {
        Console.WriteLine($"SQL Error Number: {sqlEx.Number}");
        // 2627 = Violation of PRIMARY KEY constraint
        // 547 = Foreign key violation
        // etc.
    }
}
```

**Solutions:**

1. **Configure retry policy**
   ```csharp
   RetryPolicy = new RetryPolicy
   {
       MaxRetries = 5,
       InitialDelayMs = 200,
       MaxDelayMs = 10000,
       UseExponentialBackoff = true,
       UseJitter = true
   }
   ```

2. **Validate data before ingestion**
   ```csharp
   var validData = data.Where(ValidateRow);
   ```

3. **Use idempotent inserts**
   ```sql
   -- PostgreSQL
   INSERT INTO table (...) VALUES (...)
   ON CONFLICT (id) DO NOTHING;

   -- MySQL
   INSERT IGNORE INTO table (...) VALUES (...);

   -- SQL Server
   MERGE INTO table AS target
   USING source ON target.id = source.id
   WHEN NOT MATCHED THEN INSERT ...
   ```

### Issue: CPU Usage Too High

**Symptoms:**
- CPU at 100%
- System unresponsive
- Other applications affected

**Possible Causes:**

1. **No CPU throttling**
   ```csharp
   // ❌ BAD: No limits
   EnableCpuThrottling = false

   // ✅ GOOD: Set limits
   EnableCpuThrottling = true
   MaxCpuPercent = 70.0
   ```

2. **Too much parallelism**
   ```csharp
   // ❌ BAD: Too many threads
   MaxDegreeOfParallelism = 32

   // ✅ GOOD: Reasonable parallelism
   MaxDegreeOfParallelism = Environment.ProcessorCount / 2
   ```

**Solutions:**
```csharp
var options = new BatchIngestOptions
{
    EnableCpuThrottling = true,
    MaxCpuPercent = 70.0,        // Leave headroom
    ThrottleDelayMs = 200,       // Longer delay
    MaxDegreeOfParallelism = 2,  // Reduce parallelism

    OnProgress = metrics =>
    {
        var cpu = metrics.CurrentPerformance?.CpuUsagePercent ?? 0;
        if (cpu > 90)
        {
            Console.WriteLine($"⚠️ High CPU: {cpu:F2}%");
        }
    }
};
```

## Performance Issues

### Slow Batch Processing

**Check batch duration:**
```csharp
var options = new BatchIngestOptions
{
    OnBatchCompleted = (batchNum, duration) =>
    {
        if (duration.TotalSeconds > 2)
        {
            Console.WriteLine($"⚠️ Batch {batchNum} took {duration.TotalSeconds:F2}s (expected <2s)");
        }
    }
};
```

**Investigate:**
1. Database query performance (enable query logging)
2. Network latency (ping database server)
3. Database locks (check for blocking queries)
4. Resource constraints (CPU, memory, disk IO)

### Throughput Plateaus

**Symptoms:**
- Throughput doesn't improve with more resources
- Adding more parallelism doesn't help

**Likely causes:**
1. **Database bottleneck** - Database can't keep up
2. **Network bottleneck** - Network bandwidth saturated
3. **Lock contention** - Database locks limiting concurrency

**Test:**
```csharp
// Test with different MaxDOP values
for (int maxDop = 1; maxDop <= 8; maxDop++)
{
    var options = new BatchIngestOptions
    {
        BatchSize = 2000,
        MaxDegreeOfParallelism = maxDop,
        // ... other config ...
    };

    var stopwatch = Stopwatch.StartNew();
    var metrics = await ingestor.IngestAsync(testData, "TestTable");
    stopwatch.Stop();

    Console.WriteLine($"MaxDOP={maxDop}: {metrics.RowsPerSecond:N0} rows/sec");
}
```

If throughput doesn't increase beyond a certain MaxDOP, the bottleneck is elsewhere.

## Error Scenarios

### BatchIngestException

**Example:**
```
Tika.BatchIngestor.Abstractions.Exceptions.BatchIngestException: Failed to process batch 42
  at Tika.BatchIngestor.Internal.BatchProcessor`1.ProcessBatchAsync(...)
  BatchNumber: 42
  RowsProcessedBeforeError: 41000
```

**What it means:**
- Batch #42 failed (out of all batches)
- Successfully processed 41,000 rows before this batch
- Check inner exception for root cause

**Investigation:**
```csharp
catch (BatchIngestException ex)
{
    Console.WriteLine($"Batch {ex.BatchNumber} failed");
    Console.WriteLine($"Successfully processed {ex.RowsProcessedBeforeError} rows before error");

    if (ex.InnerException is TimeoutException)
    {
        Console.WriteLine("Database timeout - consider:");
        Console.WriteLine("  - Increasing CommandTimeoutSeconds");
        Console.WriteLine("  - Reducing BatchSize");
        Console.WriteLine("  - Checking database performance");
    }
    else if (ex.InnerException is SqlException sqlEx)
    {
        Console.WriteLine($"SQL Error {sqlEx.Number}: {sqlEx.Message}");
        // Handle specific SQL errors
    }
}
```

### Connection Timeout

**Error:**
```
System.TimeoutException: Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool.
```

**Causes:**
1. Connection pool exhausted
2. Connections not being disposed
3. MaxDegreeOfParallelism > pool size

**Solution:**
```csharp
// Increase connection pool size in connection string
var connectionString = "Server=...;Database=...;" +
                       "Min Pool Size=4;" +
                       "Max Pool Size=20;" +  // Ensure >= MaxDegreeOfParallelism
                       "Connection Timeout=30";

// And/or reduce parallelism
MaxDegreeOfParallelism = 4
```

### Command Timeout

**Error:**
```
System.Data.SqlClient.SqlException: Timeout expired. The timeout period elapsed prior to completion of the operation.
```

**Causes:**
1. Batch too large for database to process in time
2. Database under heavy load
3. Network latency

**Solution:**
```csharp
var options = new BatchIngestOptions
{
    CommandTimeoutSeconds = 600,   // Increase from default 300
    BatchSize = 1000,              // Reduce batch size

    RetryPolicy = new RetryPolicy  // Add retries for timeouts
    {
        MaxRetries = 3,
        InitialDelayMs = 500,
        UseExponentialBackoff = true
    }
};
```

### Transaction Deadlock

**Error:**
```
System.Data.SqlClient.SqlException: Transaction was deadlocked on lock resources with another process.
```

**Causes:**
- Multiple processes inserting into same table
- Complex foreign key relationships
- Concurrent updates to related tables

**Solutions:**
```csharp
// 1. Use shorter transactions
var options = new BatchIngestOptions
{
    TransactionPerBatch = true,  // Not one big transaction
    BatchSize = 1000             // Smaller batches = shorter transactions
};

// 2. Add retry policy with backoff
RetryPolicy = new RetryPolicy
{
    MaxRetries = 5,
    InitialDelayMs = 100,
    MaxDelayMs = 5000,
    UseExponentialBackoff = true,
    UseJitter = true  // Helps avoid repeated deadlocks
};

// 3. Consider staging table approach (see below)
```

## Connection Issues

### Connection Pool Exhaustion

**Symptoms:**
```
System.InvalidOperationException: Timeout expired. The timeout period elapsed prior to obtaining a connection from the pool.
```

**Check pool usage:**
```csharp
// Enable connection pool logging (SQL Server)
// Add to connection string: "Application Name=MyApp"
// Monitor: select * from sys.dm_exec_connections where program_name = 'MyApp'
```

**Fix:**
```csharp
// Option 1: Increase pool size
var connectionString = "Server=...;Max Pool Size=50;...";  // Default is 100

// Option 2: Reduce parallelism
MaxDegreeOfParallelism = 4  // Should be < Max Pool Size

// Option 3: Enable connection pooling
var connectionString = "Server=...;Pooling=true;...";  // Default is true
```

### Intermittent Connection Failures

**Symptoms:**
- Random connection failures
- Works sometimes, fails other times
- More common under load

**Add resilience:**
```csharp
// 1. Enable connection retry (SQL Server)
var connectionString = "Server=...;" +
                       "ConnectRetryCount=3;" +
                       "ConnectRetryInterval=10;";

// 2. Add retry policy
RetryPolicy = new RetryPolicy
{
    MaxRetries = 5,
    InitialDelayMs = 200,
    MaxDelayMs = 15000,
    UseExponentialBackoff = true,
    UseJitter = true
};

// 3. Implement connection validation
public class ValidatingConnectionFactory : IConnectionFactory
{
    private readonly IConnectionFactory _inner;

    public async Task<DbConnection> CreateConnectionAsync(CancellationToken ct)
    {
        for (int attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                var connection = await _inner.CreateConnectionAsync(ct);

                // Validate connection
                await using var cmd = connection.CreateCommand();
                cmd.CommandText = "SELECT 1";
                await cmd.ExecuteScalarAsync(ct);

                return connection;
            }
            catch when (attempt < 3)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), ct);
            }
        }

        throw new InvalidOperationException("Failed to create valid connection after 3 attempts");
    }
}
```

## Memory Issues

### Memory Keeps Growing

**Diagnosis:**
```csharp
var options = new BatchIngestOptions
{
    EnablePerformanceMetrics = true,
    PerformanceMetricsIntervalMs = 5000,

    OnProgress = metrics =>
    {
        var perf = metrics.CurrentPerformance;
        Console.WriteLine($"Memory: {perf?.WorkingSetMB:F2} MB, Peak: {perf?.PeakWorkingSetMB:F2} MB");
        Console.WriteLine($"GC: Gen0={perf?.Gen0Collections}, Gen2={perf?.Gen2Collections}");

        // Force GC to check if memory is reclaimable
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var afterGC = GC.GetTotalMemory(false) / 1_000_000.0;
        Console.WriteLine($"After GC: {afterGC:F2} MB");
    }
};
```

**If memory doesn't decrease after GC:**
- Possible memory leak
- Check for event handler subscriptions not being cleaned up
- Check for static/long-lived references to batch data

**Solutions:**
```csharp
// 1. Reduce buffering
MaxInFlightBatches = 5  // Lower limit

// 2. Disable performance metrics if not needed
EnablePerformanceMetrics = false

// 3. Process in smaller chunks
var batchSize = 10000;
for (int i = 0; i < totalData.Count; i += batchSize)
{
    var chunk = totalData.Skip(i).Take(batchSize);
    await ingestor.IngestAsync(chunk, "MyTable");

    // Allow GC between chunks
    GC.Collect();
}
```

### Frequent GC Collections

**Check GC stats:**
```csharp
var gen0Before = GC.CollectionCount(0);
var gen1Before = GC.CollectionCount(1);
var gen2Before = GC.CollectionCount(2);

var metrics = await ingestor.IngestAsync(data, "MyTable");

Console.WriteLine($"GC Collections during ingestion:");
Console.WriteLine($"  Gen0: {GC.CollectionCount(0) - gen0Before}");
Console.WriteLine($"  Gen1: {GC.CollectionCount(1) - gen1Before}");
Console.WriteLine($"  Gen2: {GC.CollectionCount(2) - gen2Before}");
```

**If Gen2 collections are frequent (>10):**
- Reduce `MaxInFlightBatches`
- Reduce `BatchSize`
- Process data in smaller overall chunks

## Configuration Problems

### Invalid Configuration

**Error:**
```
System.ArgumentException: BatchSize must be greater than 0.
```

**Validation happens at:**
```csharp
var options = new BatchIngestOptions();
options.Validate();  // Called automatically by BatchIngestor constructor
```

**Check configuration:**
```csharp
try
{
    var options = new BatchIngestOptions
    {
        BatchSize = batchSizeFromConfig,
        MaxDegreeOfParallelism = maxDopFromConfig,
        // ... other config ...
    };

    options.Validate();
    Console.WriteLine("✓ Configuration valid");
}
catch (ArgumentException ex)
{
    Console.WriteLine($"❌ Invalid configuration: {ex.Message}");
}
```

### Configuration Not Taking Effect

**Common mistake - not using the configured options:**
```csharp
// ❌ BAD: Created options but not using them
var options = new BatchIngestOptions { BatchSize = 5000 };
var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    dialect,
    mapper,
    new BatchIngestOptions()  // Using default instead!
);

// ✅ GOOD: Using the configured options
var ingestor = new BatchIngestor<MyData>(
    connectionFactory,
    dialect,
    mapper,
    options  // Using our configuration
);
```

## Database-Specific Issues

### SQL Server: Parameter Limit Exceeded

**Error:**
```
The incoming request has too many parameters. The server supports a maximum of 2100 parameters.
```

**Cause:**
```
BatchSize × ColumnCount > 2100

Example:
BatchSize = 500
Columns = 5
Parameters = 500 × 5 = 2500 > 2100 ❌
```

**Solution:**
```csharp
// Calculate max batch size
var columnCount = 10;
var maxBatchSize = 2100 / columnCount;  // = 210

var options = new BatchIngestOptions
{
    BatchSize = Math.Min(maxBatchSize, 200)  // Use 200 to be safe
};
```

### PostgreSQL: Tuple Too Large

**Error:**
```
PostgresException: row is too big: size 10240, maximum size 8160
```

**Cause:**
- Single row exceeds PostgreSQL's page size limit

**Solutions:**
1. Split large columns into separate tables
2. Use TOAST storage for large values (automatic)
3. Reduce row size

### MySQL: Packet Too Large

**Error:**
```
MySqlException: Packet for query is too large (10485760 > 1048576)
```

**Cause:**
- Batch exceeds `max_allowed_packet` server setting

**Solutions:**
```csharp
// 1. Reduce batch size
BatchSize = 1000  // Smaller batches

// 2. Or increase server setting (requires server access)
// SET GLOBAL max_allowed_packet = 67108864;  -- 64MB
```

### SQLite: Database Locked

**Error:**
```
SQLiteException: database is locked
```

**Cause:**
- SQLite only allows one writer at a time
- Multiple parallel writers attempting to write

**Solution:**
```csharp
// SQLite: Use single writer
var options = new BatchIngestOptions
{
    MaxDegreeOfParallelism = 1,  // Must be 1 for SQLite
    MaxInFlightBatches = 1,
    UseTransactions = true,
    TransactionPerBatch = false  // Single transaction is fine for SQLite
};
```

## Getting Help

### Collect Diagnostic Information

When reporting issues, include:

```csharp
// 1. Configuration
Console.WriteLine("Configuration:");
Console.WriteLine($"  BatchSize: {options.BatchSize}");
Console.WriteLine($"  MaxDegreeOfParallelism: {options.MaxDegreeOfParallelism}");
Console.WriteLine($"  MaxInFlightBatches: {options.MaxInFlightBatches}");
Console.WriteLine($"  EnableCpuThrottling: {options.EnableCpuThrottling}");
Console.WriteLine($"  MaxCpuPercent: {options.MaxCpuPercent}");

// 2. Environment
Console.WriteLine("\nEnvironment:");
Console.WriteLine($"  OS: {Environment.OSVersion}");
Console.WriteLine($"  .NET: {Environment.Version}");
Console.WriteLine($"  Processor Count: {Environment.ProcessorCount}");
Console.WriteLine($"  Working Set: {Environment.WorkingSet / 1_000_000} MB");

// 3. Metrics
Console.WriteLine("\nMetrics:");
Console.WriteLine($"  Total Rows Processed: {metrics.TotalRowsProcessed}");
Console.WriteLine($"  Batches Completed: {metrics.BatchesCompleted}");
Console.WriteLine($"  Error Count: {metrics.ErrorCount}");
Console.WriteLine($"  Throughput: {metrics.RowsPerSecond:N0} rows/sec");
Console.WriteLine($"  Elapsed Time: {metrics.ElapsedTime}");

// 4. Exception details
Console.WriteLine("\nException:");
Console.WriteLine($"  Type: {ex.GetType().FullName}");
Console.WriteLine($"  Message: {ex.Message}");
Console.WriteLine($"  Stack Trace: {ex.StackTrace}");
if (ex.InnerException != null)
{
    Console.WriteLine($"  Inner Exception: {ex.InnerException.GetType().FullName}");
    Console.WriteLine($"  Inner Message: {ex.InnerException.Message}");
}
```

### Enable Full Diagnostic Logging

```csharp
var options = new BatchIngestOptions
{
    Logger = CreateDiagnosticLogger(),
    EnablePerformanceMetrics = true,

    OnProgress = metrics =>
    {
        // Log everything
        Console.WriteLine(JsonSerializer.Serialize(metrics, new JsonSerializerOptions
        {
            WriteIndented = true
        }));
    },

    OnBatchCompleted = (num, duration) =>
    {
        Console.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] Batch {num}: {duration.TotalMilliseconds:N2}ms");
    }
};

static ILogger CreateDiagnosticLogger()
{
    return LoggerFactory
        .Create(builder => builder
            .AddConsole()
            .AddDebug()
            .SetMinimumLevel(LogLevel.Trace))
        .CreateLogger<BatchIngestor<object>>();
}
```

### Report Issues

1. **GitHub Issues**: https://github.com/TarekFawaz/Tika.BatchIngestor/issues
2. **Include**:
   - Library version
   - Database type and version
   - Configuration used
   - Full exception with stack trace
   - Minimal reproducible example if possible

### Minimal Reproducible Example

```csharp
using Tika.BatchIngestor;
using Microsoft.Data.SqlClient;

// Minimal example demonstrating the issue
var connectionString = "Server=localhost;Database=test;...";
var connectionFactory = new SimpleConnectionFactory(
    connectionString,
    () => new SqlConnection(connectionString)
);

var options = new BatchIngestOptions
{
    BatchSize = 1000,
    MaxDegreeOfParallelism = 4
    // ... minimal config to reproduce issue
};

var testData = Enumerable.Range(1, 10000)
    .Select(i => new { Id = i, Name = $"Test{i}" });

var mapper = new DefaultRowMapper<object>(obj =>
{
    var d = (dynamic)obj;
    return new Dictionary<string, object?>
    {
        ["Id"] = d.Id,
        ["Name"] = d.Name
    };
});

var ingestor = new BatchIngestor<object>(
    connectionFactory,
    new SqlServerDialect(),
    mapper,
    options
);

try
{
    var metrics = await ingestor.IngestAsync(testData, "TestTable");
    Console.WriteLine($"Success: {metrics.TotalRowsProcessed} rows");
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex}");
}
```

## Quick Reference

### Performance Checklist
- [ ] BatchSize = 1000-5000 (adjust for your data)
- [ ] MaxDegreeOfParallelism = 2-8 (start with processor count / 2)
- [ ] MaxInFlightBatches = 10-20
- [ ] EnablePerformanceMetrics = true (for monitoring)
- [ ] CommandTimeoutSeconds = 300-600
- [ ] RetryPolicy configured with exponential backoff
- [ ] Connection pool size >= MaxDegreeOfParallelism

### Error Handling Checklist
- [ ] Retry policy configured
- [ ] Exception logging enabled
- [ ] OnProgress callback for monitoring
- [ ] OnBatchCompleted for detailed tracking
- [ ] Health checks configured (production)

### Memory Checklist
- [ ] MaxInFlightBatches <= 20
- [ ] Memory usage = MaxInFlightBatches × BatchSize × AvgRowSize < Available RAM
- [ ] EnablePerformanceMetrics = false if memory constrained
- [ ] Monitor GC Gen2 collections

## References

- [Architecture Overview](architecture.md)
- [Performance Tuning Guide](performance-tuning.md)
- [Cloud Deployment Guide](cloud-deployment.md)
- [Health Check Integration](health-checks.md)
- [Contributing Guide](CONTRIBUTING.md)
