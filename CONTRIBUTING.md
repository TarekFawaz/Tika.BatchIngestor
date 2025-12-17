# Contributing Guide

Thank you for your interest in contributing to Tika.BatchIngestor! This guide will help you get started with contributing to the project.

## Table of Contents

- [Code of Conduct](#code-of-conduct)
- [Getting Started](#getting-started)
- [Development Setup](#development-setup)
- [How to Contribute](#how-to-contribute)
- [Coding Guidelines](#coding-guidelines)
- [Testing Guidelines](#testing-guidelines)
- [Pull Request Process](#pull-request-process)
- [Adding New Features](#adding-new-features)
- [Reporting Bugs](#reporting-bugs)
- [Documentation](#documentation)

## Code of Conduct

This project adheres to a code of conduct that all contributors are expected to follow. Please be respectful and constructive in all interactions.

### Expected Behavior

- Be respectful and inclusive
- Welcome newcomers and help them get started
- Give and accept constructive feedback gracefully
- Focus on what is best for the community
- Show empathy towards others

### Unacceptable Behavior

- Harassment or discriminatory language
- Trolling or insulting comments
- Public or private harassment
- Publishing others' private information without permission

## Getting Started

### Prerequisites

- **.NET SDK 6.0 or later** - [Download](https://dotnet.microsoft.com/download)
- **Git** - [Download](https://git-scm.com/downloads)
- **IDE**: Visual Studio 2022, VS Code, or Rider
- **Database** (for testing): SQL Server, PostgreSQL, MySQL, or SQLite

### Fork and Clone

1. Fork the repository on GitHub
2. Clone your fork:
   ```bash
   git clone https://github.com/YOUR_USERNAME/Tika.BatchIngestor.git
   cd Tika.BatchIngestor
   ```

3. Add upstream remote:
   ```bash
   git remote add upstream https://github.com/TarekFawaz/Tika.BatchIngestor.git
   ```

4. Create a branch:
   ```bash
   git checkout -b feature/your-feature-name
   ```

## Development Setup

### Build the Project

```bash
# Restore dependencies
dotnet restore

# Build
dotnet build

# Run tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

### Project Structure

```
Tika.BatchIngestor/
├── src/
│   ├── Tika.BatchIngestor/              # Main library
│   ├── Tika.BatchIngestor.Abstractions/ # Interfaces and contracts
│   └── Tika.BatchIngestor.Samples/      # Usage examples
├── tests/
│   └── Tika.BatchIngestor.Tests/        # Unit and integration tests
├── benchmarks/
│   └── Tika.BatchIngestor.Benchmarks/   # Performance benchmarks
└── docs/                                 # Documentation
```

### Running Tests

#### Unit Tests

```bash
cd tests/Tika.BatchIngestor.Tests
dotnet test
```

#### Integration Tests

Integration tests require a database. Set up a test database and configure the connection string:

```bash
# Using SQLite (easiest for local testing)
dotnet test

# Using PostgreSQL
export TEST_CONNECTION_STRING="Host=localhost;Database=test;Username=postgres;Password=postgres"
dotnet test --filter "Category=PostgreSQL"

# Using SQL Server
export TEST_CONNECTION_STRING="Server=localhost;Database=test;Integrated Security=true"
dotnet test --filter "Category=SqlServer"
```

#### Benchmarks

```bash
cd benchmarks/Tika.BatchIngestor.Benchmarks
dotnet run -c Release
```

## How to Contribute

### Types of Contributions

1. **Bug Fixes** - Fix existing bugs
2. **New Features** - Add new functionality
3. **Performance Improvements** - Optimize existing code
4. **Documentation** - Improve or add documentation
5. **Tests** - Add or improve test coverage
6. **Examples** - Add usage examples

### Finding Issues to Work On

- Look for issues labeled `good first issue` for newcomers
- Check issues labeled `help wanted` for areas needing contribution
- Browse open issues and propose solutions

### Proposing Changes

For significant changes:

1. **Open an issue first** to discuss your proposal
2. Get feedback from maintainers
3. Proceed with implementation once approved

For small changes (typos, small bug fixes):
- You can submit a PR directly

## Coding Guidelines

### C# Style

We follow standard C# coding conventions with some specific guidelines:

#### Naming Conventions

```csharp
// Classes: PascalCase
public class BatchIngestor<T> { }

// Interfaces: PascalCase with 'I' prefix
public interface IBatchIngestor<T> { }

// Methods: PascalCase
public async Task<BatchIngestMetrics> IngestAsync() { }

// Properties: PascalCase
public int BatchSize { get; set; }

// Private fields: camelCase with underscore prefix
private readonly IConnectionFactory _connectionFactory;

// Local variables: camelCase
var batchSize = 1000;

// Constants: PascalCase
private const int DefaultBatchSize = 1000;
```

#### Code Style

```csharp
// Use var when type is obvious
var metrics = new BatchIngestMetrics();
var connection = await factory.CreateConnectionAsync();

// Explicit type when not obvious
IConnectionFactory factory = GetFactory();

// Prefer expression-bodied members for simple properties
public string Name => _name;

// Use braces for control statements (even single line)
if (condition)
{
    DoSomething();
}

// Async methods should end with 'Async'
public async Task ProcessAsync() { }

// Use null-conditional and null-coalescing operators
var value = obj?.Property ?? defaultValue;

// Use pattern matching
if (exception is SqlException sqlEx)
{
    // Handle SQL exception
}
```

#### Documentation

```csharp
/// <summary>
/// Ingests data into the specified table.
/// </summary>
/// <typeparam name="T">The type of data to ingest.</typeparam>
/// <param name="data">The data to ingest.</param>
/// <param name="tableName">The target table name.</param>
/// <param name="cancellationToken">Cancellation token.</param>
/// <returns>Metrics about the ingestion operation.</returns>
/// <exception cref="ArgumentNullException">Thrown when data or tableName is null.</exception>
/// <exception cref="BatchIngestException">Thrown when a batch fails to process.</exception>
public async Task<BatchIngestMetrics> IngestAsync(
    IAsyncEnumerable<T> data,
    string tableName,
    CancellationToken cancellationToken = default)
{
    // Implementation
}
```

### Performance Guidelines

1. **Use async/await properly**
   ```csharp
   // ✅ Good
   await connection.OpenAsync(cancellationToken);

   // ❌ Bad
   connection.OpenAsync(cancellationToken).Wait();
   ```

2. **Avoid allocations in hot paths**
   ```csharp
   // ✅ Good: Reuse StringBuilder
   private readonly StringBuilder _builder = new();

   // ❌ Bad: New instance each time
   var builder = new StringBuilder();
   ```

3. **Use lock-free operations when possible**
   ```csharp
   // ✅ Good: Lock-free
   Interlocked.Add(ref _counter, value);

   // ❌ Bad: Lock-based
   lock (_lock) { _counter += value; }
   ```

4. **Dispose resources properly**
   ```csharp
   // ✅ Good
   await using var connection = await factory.CreateConnectionAsync();

   // ✅ Also good
   using var command = connection.CreateCommand();
   ```

### Error Handling

```csharp
// Validate arguments
public BatchIngestor(IConnectionFactory connectionFactory)
{
    _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
}

// Use specific exception types
if (batchSize <= 0)
{
    throw new ArgumentException("BatchSize must be greater than 0.", nameof(batchSize));
}

// Wrap exceptions with context
catch (Exception ex)
{
    throw new BatchIngestException(
        $"Failed to process batch {batchNumber}",
        batchNumber,
        rowsProcessed,
        ex);
}
```

## Testing Guidelines

### Writing Tests

We use xUnit for testing. Follow these guidelines:

#### Test Structure

```csharp
public class BatchIngestorTests
{
    [Fact]
    public async Task IngestAsync_WithValidData_ReturnsMetrics()
    {
        // Arrange
        var connectionFactory = CreateMockConnectionFactory();
        var options = new BatchIngestOptions { BatchSize = 100 };
        var ingestor = new BatchIngestor<TestData>(
            connectionFactory,
            new GenericSqlDialect(),
            new TestDataMapper(),
            options);

        var testData = GenerateTestData(1000);

        // Act
        var metrics = await ingestor.IngestAsync(testData, "TestTable");

        // Assert
        Assert.Equal(1000, metrics.TotalRowsProcessed);
        Assert.Equal(10, metrics.BatchesCompleted);
        Assert.Equal(0, metrics.ErrorCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Constructor_WithInvalidBatchSize_ThrowsArgumentException(int batchSize)
    {
        // Arrange
        var options = new BatchIngestOptions { BatchSize = batchSize };

        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            new BatchIngestor<TestData>(
                CreateMockConnectionFactory(),
                new GenericSqlDialect(),
                new TestDataMapper(),
                options));
    }
}
```

#### Test Categories

Use categories to organize tests:

```csharp
[Trait("Category", "Unit")]
public class UnitTests { }

[Trait("Category", "Integration")]
public class IntegrationTests { }

[Trait("Category", "Performance")]
public class PerformanceTests { }

[Trait("Category", "SqlServer")]
public class SqlServerTests { }
```

#### Test Data

```csharp
// Use realistic test data
private static IEnumerable<SensorData> GenerateTestData(int count)
{
    return Enumerable.Range(1, count)
        .Select(i => new SensorData
        {
            SensorId = $"SENSOR-{i:D6}",
            Timestamp = DateTime.UtcNow.AddSeconds(-i),
            Temperature = 20.0 + (i % 50),
            Humidity = 40.0 + (i % 60)
        });
}
```

#### Mocking

```csharp
// Create test doubles for dependencies
public class FakeConnectionFactory : IConnectionFactory
{
    private readonly DbConnection _connection;

    public FakeConnectionFactory(DbConnection connection)
    {
        _connection = connection;
    }

    public Task<DbConnection> CreateConnectionAsync(CancellationToken ct)
    {
        return Task.FromResult(_connection);
    }
}
```

### Test Coverage

- Aim for **>80% code coverage**
- Cover happy paths and error cases
- Include edge cases (null, empty, boundary values)
- Test concurrent scenarios

```bash
# Generate coverage report
dotnet test --collect:"XPlat Code Coverage"

# View coverage (using ReportGenerator)
reportgenerator -reports:"**/coverage.cobertura.xml" -targetdir:"coveragereport"
```

## Pull Request Process

### Before Submitting

1. **Ensure all tests pass**
   ```bash
   dotnet test
   ```

2. **Build succeeds**
   ```bash
   dotnet build
   ```

3. **Code follows style guidelines**
   - Run code formatter if available
   - Check for warnings

4. **Update documentation**
   - Update README if needed
   - Add XML comments for public APIs
   - Update relevant docs/ files

5. **Add tests for new functionality**

### Commit Messages

Use clear, descriptive commit messages:

```
feat: Add support for Aurora MySQL dialect

- Implement AuroraMySqlDialect class
- Add Aurora-specific optimizations
- Include integration tests
- Update documentation

Fixes #123
```

**Format:**
```
<type>: <subject>

<body>

<footer>
```

**Types:**
- `feat`: New feature
- `fix`: Bug fix
- `docs`: Documentation changes
- `test`: Adding or updating tests
- `refactor`: Code refactoring
- `perf`: Performance improvements
- `chore`: Maintenance tasks
- `build`: Build system changes

### Creating the Pull Request

1. **Push to your fork**
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Create PR on GitHub**
   - Go to your fork on GitHub
   - Click "New Pull Request"
   - Select your branch
   - Fill in PR template

3. **PR Description should include:**
   - What changes were made
   - Why the changes were necessary
   - How to test the changes
   - Any breaking changes
   - Related issues

**Example:**
```markdown
## Description
Adds support for Amazon Aurora MySQL with optimized dialect.

## Motivation
Users deploying on AWS Aurora MySQL need optimized SQL generation for better performance.

## Changes
- Added `AuroraMySqlDialect` class
- Implemented Aurora-specific query optimizations
- Added integration tests for Aurora MySQL
- Updated cloud deployment documentation

## Testing
- Unit tests: `dotnet test --filter "ClassName=AuroraMySqlDialectTests"`
- Integration tests: Requires Aurora MySQL instance
- Benchmarks show 15% throughput improvement

## Breaking Changes
None

## Related Issues
Closes #123
```

### Review Process

1. **Automated checks will run**
   - Build
   - Tests
   - Code coverage

2. **Maintainers will review**
   - Code quality
   - Test coverage
   - Documentation
   - Design decisions

3. **Address feedback**
   - Make requested changes
   - Push updates to same branch
   - Reply to review comments

4. **Approval and merge**
   - Once approved, maintainers will merge
   - PR will be linked in release notes

## Adding New Features

### SQL Dialects

To add a new SQL dialect:

1. **Create dialect class**
   ```csharp
   namespace Tika.BatchIngestor.Dialects;

   /// <summary>
   /// SQL dialect for Oracle Database.
   /// </summary>
   public class OracleDialect : ISqlDialect
   {
       public string QuoteIdentifier(string identifier)
       {
           return $"\"{identifier}\"";
       }

       public string GetParameterName(int index)
       {
           return $":p{index}";
       }

       public int GetMaxParametersPerCommand()
       {
           return 32767;
       }

       public string BuildMultiRowInsert(
           string tableName,
           IReadOnlyList<string> columns,
           int rowCount)
       {
           // Oracle-specific INSERT ALL syntax
           var sb = new StringBuilder("INSERT ALL ");

           for (int i = 0; i < rowCount; i++)
           {
               sb.Append($"INTO {QuoteIdentifier(tableName)} (");
               sb.Append(string.Join(", ", columns.Select(QuoteIdentifier)));
               sb.Append(") VALUES (");
               sb.Append(string.Join(", ",
                   Enumerable.Range(0, columns.Count)
                       .Select(j => GetParameterName(i * columns.Count + j))));
               sb.Append(") ");
           }

           sb.Append("SELECT 1 FROM DUAL");
           return sb.ToString();
       }
   }
   ```

2. **Add tests**
   ```csharp
   public class OracleDialectTests
   {
       [Fact]
       public void QuoteIdentifier_WithValidIdentifier_ReturnsQuoted()
       {
           var dialect = new OracleDialect();
           var result = dialect.QuoteIdentifier("MyTable");
           Assert.Equal("\"MyTable\"", result);
       }

       [Fact]
       public void BuildMultiRowInsert_WithMultipleRows_GeneratesCorrectSql()
       {
           var dialect = new OracleDialect();
           var columns = new List<string> { "Id", "Name" };

           var sql = dialect.BuildMultiRowInsert("Users", columns, 2);

           Assert.Contains("INSERT ALL", sql);
           Assert.Contains("INTO \"Users\"", sql);
           Assert.Contains("SELECT 1 FROM DUAL", sql);
       }
   }
   ```

3. **Add integration tests** (if database available)

4. **Update documentation**
   - Add to README supported databases table
   - Add example usage
   - Update cloud deployment guide if applicable

### Bulk Insert Strategies

To add a custom bulk insert strategy:

1. **Implement interface**
   ```csharp
   public class PostgresCopyStrategy<T> : IBulkInsertStrategy<T>
   {
       public async Task<int> ExecuteAsync(
           DbConnection connection,
           string tableName,
           IReadOnlyList<string> columns,
           IReadOnlyList<T> rows,
           IRowMapper<T> mapper,
           CancellationToken cancellationToken)
       {
           if (connection is not NpgsqlConnection npgsqlConnection)
           {
               throw new InvalidOperationException(
                   "PostgresCopyStrategy requires NpgsqlConnection");
           }

           // Use PostgreSQL COPY command for maximum performance
           await using var writer = await npgsqlConnection.BeginBinaryImportAsync(
               $"COPY {tableName} ({string.Join(", ", columns)}) FROM STDIN BINARY",
               cancellationToken);

           foreach (var row in rows)
           {
               var mapped = mapper.Map(row);
               await writer.StartRowAsync(cancellationToken);

               foreach (var column in columns)
               {
                   await writer.WriteAsync(mapped[column], cancellationToken);
               }
           }

           await writer.CompleteAsync(cancellationToken);
           return rows.Count;
       }
   }
   ```

2. **Add tests and documentation**

## Reporting Bugs

### Before Reporting

1. **Search existing issues** to avoid duplicates
2. **Verify it's reproducible** with latest version
3. **Simplify** to minimal reproduction case

### Bug Report Template

```markdown
## Description
Clear description of the bug.

## Steps to Reproduce
1. Step one
2. Step two
3. Step three

## Expected Behavior
What you expected to happen.

## Actual Behavior
What actually happened.

## Minimal Reproduction
```csharp
// Minimal code that reproduces the issue
var options = new BatchIngestOptions { BatchSize = 1000 };
var ingestor = new BatchIngestor<MyData>(...);
await ingestor.IngestAsync(data, "MyTable");
// Error occurs here
```

## Environment
- Library Version: 1.0.0
- .NET Version: 6.0.0
- Database: PostgreSQL 14
- OS: Windows 10

## Exception (if applicable)
```
Full exception with stack trace
```

## Additional Context
Any other relevant information.
```

## Documentation

### Documentation Structure

- **README.md** - Main documentation, quick start
- **docs/architecture.md** - Architecture overview
- **docs/performance-tuning.md** - Performance guide
- **docs/cloud-deployment.md** - Cloud deployment
- **docs/health-checks.md** - Health check integration
- **docs/troubleshooting.md** - Troubleshooting guide
- **CONTRIBUTING.md** - This file

### Updating Documentation

When adding features:

1. **Update README** if it affects quick start or main features
2. **Add to appropriate docs/** file
3. **Update XML comments** in code
4. **Add code examples** demonstrating usage

### Writing Good Documentation

- **Be clear and concise**
- **Include code examples**
- **Explain why, not just what**
- **Use proper formatting**
- **Test all code examples**

## Community

### Getting Help

- **GitHub Discussions** - Ask questions, share ideas
- **GitHub Issues** - Report bugs, request features
- **Documentation** - Check docs/ folder

### Staying Updated

- Watch the repository for updates
- Check release notes for changes
- Follow discussions for decisions

## Recognition

Contributors will be recognized in:
- Release notes
- README contributors section
- Project documentation

Thank you for contributing to Tika.BatchIngestor! 🎉
