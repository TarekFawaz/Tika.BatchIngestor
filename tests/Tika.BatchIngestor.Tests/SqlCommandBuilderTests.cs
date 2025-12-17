using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Dialects;
using Tika.BatchIngestor.Internal;
using Tika.BatchIngestor.Tests.Fakes;
using Xunit;

namespace Tika.BatchIngestor.Tests;

public class SqlCommandBuilderTests
{
    [Fact]
    public async Task BuildInsertCommand_GeneratesCorrectSql()
    {
        var dialect = new GenericSqlDialect();
        var options = new BatchIngestOptions();
        var builder = new SqlCommandBuilder(dialect, options);
        
        var mapper = new DefaultRowMapper<TestRecord>(r => new Dictionary<string, object?>
        {
            ["Id"] = r.Id,
            ["Name"] = r.Name
        });

        var rows = new List<TestRecord>
        {
            new() { Id = 1, Name = "Alice" },
            new() { Id = 2, Name = "Bob" }
        };

        var columns = new List<string> { "Id", "Name" };

        await using var connection = new FakeDbConnection();
        await connection.OpenAsync();

        using var command = builder.BuildInsertCommand(connection, "TestTable", columns, rows, mapper);

        Assert.Contains("INSERT INTO \"TestTable\"", command.CommandText);
        Assert.Contains("(\"Id\", \"Name\")", command.CommandText);
        Assert.Contains("VALUES", command.CommandText);
        Assert.Equal(4, command.Parameters.Count);
    }

    private class TestRecord
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }
}
