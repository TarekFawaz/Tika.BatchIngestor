# Implementing a Custom Bulk Insert Strategy

Use this prompt when contributing a custom bulk insert strategy (e.g., SqlBulkCopy, PostgreSQL COPY, etc.).

## Task

I need to implement a custom bulk insert strategy for [STRATEGY_NAME] that uses [NATIVE_API_NAME] for better performance.

### Strategy Details
- **Strategy Name**: [e.g., SqlBulkCopy, PostgreSQL COPY]
- **Target Database**: [e.g., SQL Server, PostgreSQL]
- **Expected Performance**: [e.g., 50,000-100,000 rows/sec]
- **Advantages over GenericInsertStrategy**: [describe benefits]
- **Limitations**: [describe any limitations or requirements]

### Requirements

1. Create a new strategy class in `src/Tika.BatchIngestor/Strategies/[StrategyName]Strategy.cs`
2. Implement `IBulkInsertStrategy<T>`:
   ```csharp
   Task<int> ExecuteAsync(
       DbConnection connection,
       string tableName,
       IReadOnlyList<string> columns,
       IReadOnlyList<T> rows,
       IRowMapper<T> mapper,
       CancellationToken cancellationToken);
   ```

3. Handle edge cases:
   - Empty batch (rows.Count == 0)
   - Column mapping mismatches
   - Type conversion issues
   - Transaction coordination (if applicable)

4. Optimize for throughput:
   - Minimize allocations
   - Batch size recommendations
   - Connection/transaction reuse

### Template

```csharp
using System.Data.Common;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Strategies;

/// <summary>
/// Bulk insert strategy using [NATIVE_API_NAME] for [DATABASE_NAME].
/// Provides significantly higher throughput than parameterized multi-row inserts.
/// </summary>
/// <remarks>
/// Performance characteristics:
/// - Typical throughput: [X] rows/sec
/// - Recommended batch size: [Y]
/// - Memory overhead: [description]
///
/// Requirements:
/// - [List any prerequisites, e.g., specific database permissions]
///
/// Limitations:
/// - [List any limitations]
/// </remarks>
public class [StrategyName]Strategy<T> : IBulkInsertStrategy<T>
{
    public async Task<int> ExecuteAsync(
        DbConnection connection,
        string tableName,
        IReadOnlyList<string> columns,
        IReadOnlyList<T> rows,
        IRowMapper<T> mapper,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
            return 0;

        // Implementation here
        throw new NotImplementedException();
    }
}
```

### Usage Example

```csharp
// Create custom strategy
var strategy = new [StrategyName]Strategy<Customer>();

// Use with BatchIngestor
var ingestor = new BatchIngestor<Customer>(
    connectionFactory,
    new [DatabaseName]Dialect(),
    mapper,
    options,
    strategy  // Custom strategy
);

var metrics = await ingestor.IngestAsync(data, "Customers");
```

### Testing

Create corresponding test file:

```csharp
public class [StrategyName]StrategyTests
{
    [Fact]
    public async Task ExecuteAsync_InsertsAllRows()
    {
        // Test successful insertion
    }

    [Fact]
    public async Task ExecuteAsync_HandlesEmptyBatch()
    {
        // Test empty batch
    }

    [Fact]
    public async Task ExecuteAsync_HandlesTypeMappingCorrectly()
    {
        // Test type conversion
    }
}
```

### Benchmarking

Add a benchmark to compare against GenericInsertStrategy:

```csharp
[MemoryDiagnoser]
public class StrategyComparisonBenchmarks
{
    [Benchmark(Baseline = true)]
    public async Task<BatchIngestMetrics> GenericInsertStrategy()
    {
        // Test with GenericInsertStrategy
    }

    [Benchmark]
    public async Task<BatchIngestMetrics> [StrategyName]Strategy()
    {
        // Test with your strategy
    }
}
```

## Example Strategies to Reference

- `GenericInsertStrategy.cs`: Standard parameterized multi-row inserts
- `BulkInsertStrategyAdapter.cs`: Adapter for synchronous bulk operations

## Common Pitfalls

1. **Transaction Handling**: Ensure your strategy respects `options.UseTransactions` and `options.TransactionPerBatch`.
2. **Column Ordering**: The order of columns must match between `columns` list and the mapped data.
3. **Type Mapping**: Different bulk APIs have different type mapping requirements (e.g., SqlBulkCopy requires exact type matches).
4. **NULL Handling**: Some bulk APIs require explicit NULL value handling.
5. **Connection State**: Ensure the connection is open before using the bulk API.
6. **Cancellation**: Propagate `CancellationToken` through all async operations.

## Performance Guidelines

1. **Batch Size**: Larger batches typically work better with native bulk APIs. Recommend 5,000-50,000 rows per batch depending on row size.
2. **Memory**: Native bulk APIs often buffer data in memory. Document memory requirements.
3. **Transactions**: Some bulk APIs have special transaction requirements or optimizations.
4. **Indexes**: Bulk operations may be faster with indexes disabled/rebuilt afterward.

## Documentation Requirements

1. Add usage example to README.md
2. Document performance characteristics in class XML docs
3. Include configuration recommendations
4. Note any database version requirements
5. Explain when to use this strategy vs. GenericInsertStrategy

Please implement the strategy following these guidelines and best practices from the existing codebase.
