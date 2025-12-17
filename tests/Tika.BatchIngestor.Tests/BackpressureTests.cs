using System.Diagnostics;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Dialects;
using Tika.BatchIngestor.Tests.Fakes;
using Xunit;

namespace Tika.BatchIngestor.Tests;

public class BackpressureTests
{
    [Fact]
    public async Task IngestAsync_DoesNotBufferUnboundedData()
    {
        var connectionFactory = new FakeConnectionFactory(delayMs: 100);
        var dialect = new GenericSqlDialect();
        var mapper = new DefaultRowMapper<TestRecord>(r => new Dictionary<string, object?>
        {
            ["Id"] = r.Id,
            ["Value"] = r.Value
        });

        var options = new BatchIngestOptions
        {
            BatchSize = 10,
            MaxDegreeOfParallelism = 1,
            MaxInFlightBatches = 2
        };

        var ingestor = new BatchIngestor<TestRecord>(
            connectionFactory,
            dialect,
            mapper,
            options);

        var producer = new SlowProducer();
        var data = producer.GenerateDataAsync(1000);

        var metrics = await ingestor.IngestAsync(data, "TestTable");

        Assert.Equal(1000, metrics.TotalRowsProcessed);
        Assert.True(producer.WasThrottled, "Producer should have been throttled by backpressure");
    }

    private class TestRecord
    {
        public int Id { get; set; }
        public string Value { get; set; } = string.Empty;
    }

    private class SlowProducer
    {
        public bool WasThrottled { get; private set; }
        private int _generatedCount;
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();

        public async IAsyncEnumerable<TestRecord> GenerateDataAsync(int count)
        {
            for (int i = 0; i < count; i++)
            {
                _generatedCount = i + 1;
                
                if (_generatedCount > 50 && _stopwatch.ElapsedMilliseconds < 1000)
                {
                    // Still generating fast
                }
                else if (_generatedCount > 50)
                {
                    WasThrottled = true;
                }

                yield return new TestRecord
                {
                    Id = i,
                    Value = $"Value{i}"
                };

                await Task.Yield();
            }
        }
    }
}
