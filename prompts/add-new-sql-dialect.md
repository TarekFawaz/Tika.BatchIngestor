# Adding a New SQL Dialect

Use this prompt when contributing a new SQL dialect to Tika.BatchIngestor.

## Task

I need to add a new SQL dialect for [DATABASE_NAME] to Tika.BatchIngestor. Please help me implement the `ISqlDialect` interface with the following requirements:

### Database Characteristics
- **Database Name**: [e.g., Oracle, CockroachDB, Firebird]
- **Identifier Quoting**: [e.g., double quotes, backticks, square brackets]
- **Parameter Syntax**: [e.g., @p0, $1, :param1]
- **Max Parameters Per Command**: [typical value for this database]
- **Multi-Row INSERT Syntax**: [describe if it differs from standard SQL]

### Requirements

1. Create a new dialect class in `src/Tika.BatchIngestor/Dialects/[DatabaseName]Dialect.cs`
2. Implement all methods from `ISqlDialect`:
   - `QuoteIdentifier(string identifier)`: How to escape table/column names
   - `GetParameterName(int index)`: How to format parameter placeholders
   - `GetMaxParametersPerCommand()`: Database-specific limit
   - `BuildMultiRowInsert(...)`: Generate multi-row INSERT statement

3. Optimize for the database's specific characteristics:
   - Connection pooling behavior
   - Transaction handling
   - Network latency (cloud vs local)
   - Parameter limits

4. Add comprehensive XML documentation
5. Include usage example in the class documentation

### Template

```csharp
using System.Text;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Dialects;

/// <summary>
/// SQL dialect for [DATABASE_NAME].
/// [Describe any special considerations or optimizations]
/// </summary>
public class [DatabaseName]Dialect : ISqlDialect
{
    // Implementation here
}
```

### Testing

Create corresponding test file in `tests/Tika.BatchIngestor.Tests/Dialects/[DatabaseName]DialectTests.cs`:

```csharp
public class [DatabaseName]DialectTests
{
    [Fact]
    public void QuoteIdentifier_EscapesCorrectly()
    {
        // Test identifier quoting
    }

    [Fact]
    public void BuildMultiRowInsert_GeneratesCorrectSQL()
    {
        // Test SQL generation
    }

    [Fact]
    public void GetMaxParametersPerCommand_ReturnsExpectedValue()
    {
        // Test parameter limit
    }
}
```

### Documentation

Update README.md to include the new dialect in the "Supported Databases" table.

## Example Dialects to Reference

- `SqlServerDialect.cs`: SQL Server / Azure SQL syntax
- `GenericSqlDialect.cs`: Standard SQL with double-quoted identifiers
- `AuroraPostgreSqlDialect.cs`: Cloud-optimized PostgreSQL variant
- `AuroraMySqlDialect.cs`: Cloud-optimized MySQL variant

## Common Pitfalls

1. **Parameter Limits**: Be conservative. If the database supports 65535 parameters, use 32767 to leave headroom.
2. **Quoting**: Always quote identifiers to handle reserved keywords and special characters.
3. **NULL Handling**: Let `SqlCommandBuilder` handle NULL → DBNull conversion.
4. **Cloud Optimization**: Cloud databases benefit from slightly smaller batches due to network latency.

## Performance Considerations

- Batch size recommendations based on typical network latency
- Connection pooling best practices for the database
- Transaction handling (READ COMMITTED vs SERIALIZABLE)
- Any database-specific tuning parameters

Please implement the dialect following these guidelines and best practices from the existing codebase.
