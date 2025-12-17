using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Dialects;
using Tika.BatchIngestor.Tests.Fakes;
using Xunit;

namespace Tika.BatchIngestor.Tests;

public class BatchProcessorTests
{
    [Fact]
    public async Task IngestAsync_ProcessesAllRows()
    {
        var connectionFactory = new FakeConnectionFactory();
        var dialect = new GenericSqlDialect();
        var mapper = new DefaultRowMapper<TestRecord>(r => new Dictionary<string, object?>
        {
            ["Id"] = r.Id,
            ["Name"] = r.Name
        });

        var options = new BatchIngestOptions
        {
            BatchSize = 10,
            MaxDegreeOfParallelism = 2,
            MaxInFlightBatches = 5
        };

        var ingestor = new BatchIngestor<TestRecord>(
            connectionFactory,
            dialect,
            mapper,
            options);

        var data = GenerateTestData(100);

        var metrics = await ingestor.IngestAsync(data, "TestTable");

        Assert.Equal(100, metrics.TotalRowsProcessed);
        Assert.Equal(10, metrics.BatchesCompleted);
        Assert.True(metrics.RowsPerSecond > 0);
    }

    [Fact]
    public async Task IngestAsync_HandlesEmptyData()
    {
        var connectionFactory = new FakeConnectionFactory();
        var dialect = new GenericSqlDialect();
        var mapper = new DefaultRowMapper<TestRecord>(r => new Dictionary<string, object?>
        {
            ["Id"] = r.Id,
            ["Name"] = r.Name
        });

        var options = new BatchIngestOptions
        {
            BatchSize = 10
        };

        var ingestor = new BatchIngestor<TestRecord>(
            connectionFactory,
            dialect,
            mapper,
            options);

        var data = Array.Empty<TestRecord>();

        var metrics = await ingestor.IngestAsync(data, "TestTable");

        Assert.Equal(0, metrics.TotalRowsProcessed);
        Assert.Equal(0, metrics.BatchesCompleted);
    }

    private static async IAsyncEnumerable<TestRecord> GenerateTestData(int count)
    {
        for (int i = 0; i < count; i++)
        {
            yield return new TestRecord { Id = i, Name = $"Record{i}" };
            await Task.Yield();
        }
    }

    private class TestRecord
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
