# Contributor Prompts

This directory contains AI-assisted development prompts to help contributors extend and improve Tika.BatchIngestor.

## Available Prompts

### 🔌 [Adding a New SQL Dialect](add-new-sql-dialect.md)
Use this prompt when implementing support for a new database system (Oracle, CockroachDB, etc.). Includes:
- Template for implementing `ISqlDialect`
- Database-specific optimization guidelines
- Testing requirements
- Documentation standards

### 🚀 [Implementing a Custom Bulk Strategy](implement-custom-bulk-strategy.md)
Use this prompt when creating a native bulk insert strategy (SqlBulkCopy, PostgreSQL COPY, etc.). Includes:
- Template for implementing `IBulkInsertStrategy<T>`
- Performance optimization patterns
- Benchmarking requirements
- Trade-off analysis

### ⚡ [Performance Optimization Guide](performance-optimization.md)
Use this prompt when working on performance improvements. Includes:
- Profiling and analysis workflow
- Common optimization patterns (lock-free, zero-allocation, etc.)
- Benchmarking requirements
- Performance testing checklist

### 🧪 [Testing and Benchmarking Guide](testing-and-benchmarking.md)
Use this prompt when writing tests or benchmarks. Includes:
- Unit testing standards
- Integration testing patterns
- Benchmark structure and best practices
- IoT-specific test scenarios

## How to Use These Prompts

### For Human Contributors

1. Choose the relevant prompt file for your contribution
2. Read through the guidelines and requirements
3. Follow the templates and examples provided
4. Ensure your implementation meets all checklist items
5. Run tests and benchmarks before submitting PR

### For AI-Assisted Development

Copy the entire content of the relevant prompt file and provide it to your AI assistant along with specific details about what you're implementing:

```
[Paste content of add-new-sql-dialect.md]

I need to implement a dialect for CockroachDB with the following characteristics:
- Identifier Quoting: Double quotes
- Parameter Syntax: $1, $2, $3
- Max Parameters: 65535
- Special consideration: Distributed SQL, cloud-native
```

## Contribution Workflow

1. **Choose Your Task**
   - Adding database support → Use SQL Dialect prompt
   - Performance improvement → Use Performance Optimization prompt
   - Native bulk API → Use Bulk Strategy prompt
   - Writing tests → Use Testing prompt

2. **Follow the Prompt**
   - Read requirements carefully
   - Use provided templates
   - Follow naming conventions
   - Include comprehensive tests

3. **Validate Your Work**
   - Run all existing tests: `dotnet test`
   - Run benchmarks: `cd benchmarks && dotnet run -c Release`
   - Check code coverage
   - Verify no performance regressions

4. **Submit Pull Request**
   - Include benchmark results
   - Document any breaking changes
   - Update README.md if adding new features
   - Add usage examples

## Performance Targets

All contributions should maintain or improve these targets:

| Metric | Target | Notes |
|--------|--------|-------|
| **Throughput** | 5,000-15,000 rows/sec | Varies by row size and scenario |
| **CPU Usage** | < 80% | With throttling enabled (configurable) |
| **Memory** | Linear with batch size | Not total row count |
| **GC Gen2** | < 5 per 100k rows | Minimal Gen2 collections |
| **Latency** | < 100ms avg batch | Depends on network and DB |

## Quality Standards

### Code Quality
- ✅ Follows existing code style
- ✅ Comprehensive XML documentation
- ✅ Meaningful variable names
- ✅ No compiler warnings
- ✅ Passes all analyzers

### Testing
- ✅ Unit tests for all public APIs
- ✅ Integration tests for database operations
- ✅ Edge case coverage
- ✅ Thread safety tests (if applicable)
- ✅ > 80% code coverage

### Performance
- ✅ Benchmarks vs baseline
- ✅ Memory profiling results
- ✅ No performance regressions
- ✅ Meets throughput targets
- ✅ CPU usage within limits

### Documentation
- ✅ Updated README.md
- ✅ Usage examples
- ✅ Configuration guidelines
- ✅ Performance characteristics documented
- ✅ Breaking changes noted

## Getting Help

- 📖 Read the [main README](../README.md) for architecture overview
- 💬 Open a [Discussion](https://github.com/TarekFawaz/Tika.BatchIngestor/discussions) for questions
- 🐛 Report issues on [GitHub Issues](https://github.com/TarekFawaz/Tika.BatchIngestor/issues)
- 📧 Contact maintainers for significant contributions

## Examples of Good Contributions

1. **SQL Dialect**: See `AuroraPostgreSqlDialect.cs`
   - Clear documentation
   - Cloud-optimized parameter limits
   - Complete implementation
   - Usage examples in README

2. **Performance Optimization**: See `BatchIngestMetrics.cs`
   - Lock-free atomic operations
   - Benchmarks showing 40% improvement
   - No breaking changes
   - Comprehensive testing

3. **Health Checks**: See `BatchIngestorHealthCheck.cs`
   - ASP.NET Core integration
   - Clear documentation
   - Usage examples
   - Production-ready

## Prompt Versioning

These prompts are versioned with the library. When making significant changes to library architecture:

1. Update relevant prompts
2. Increment prompt version in commit message
3. Update examples to match current API
4. Ensure backward compatibility guidance

---

**Thank you for contributing to Tika.BatchIngestor!** 🎉

Your improvements help teams worldwide build high-performance data ingestion pipelines.
