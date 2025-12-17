using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Tika.BatchIngestor;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Dialects;
using Tika.BatchIngestor.Factories;
using Tika.BatchIngestor.Samples;
using Tika.BatchIngestor.Samples.Models;

Console.WriteLine("=== Tika.BatchIngestor Sample Application ===\n");

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Information);
});
var logger = loggerFactory.CreateLogger<Program>();

var connectionString = "Data Source=:memory:";
var connectionFactory = new SimpleConnectionFactory(
    connectionString,
    () => new SqliteConnection());

await using (var setupConn = await connectionFactory.CreateConnectionAsync())
{
    using var cmd = setupConn.CreateCommand();
    cmd.CommandText = @"
        CREATE TABLE Customers (
            Id INTEGER PRIMARY KEY,
            Name TEXT NOT NULL,
            Email TEXT NOT NULL,
            City TEXT,
            CreatedAt TEXT NOT NULL
        )";
    await cmd.ExecuteNonQueryAsync();
    logger.LogInformation("Created Customers table");
}

var options = new BatchIngestOptions
{
    BatchSize = 5000,
    MaxDegreeOfParallelism = 4,
    MaxInFlightBatches = 10,
    UseTransactions = true,
    TransactionPerBatch = true,
    CommandTimeoutSeconds = 300,
    RetryPolicy = new RetryPolicy
    {
        MaxRetries = 3,
        InitialDelayMs = 100,
        UseExponentialBackoff = true,
        UseJitter = true
    },
    OnProgress = metrics =>
    {
        Console.WriteLine(
            $"Progress: {metrics.TotalRowsProcessed:N0} rows | " +
            $"{metrics.RowsPerSecond:N0} rows/sec | " +
            $"Batches: {metrics.BatchesCompleted}");
    },
    OnBatchCompleted = (batchNum, duration) =>
    {
        if (batchNum % 50 == 0)
        {
            logger.LogDebug("Batch {BatchNumber} completed in {Duration:N2}ms",
                batchNum, duration.TotalMilliseconds);
        }
    },
    Logger = logger
};

var mapper = new DefaultRowMapper<CustomerRecord>(customer => new Dictionary<string, object?>
{
    ["Id"] = customer.Id,
    ["Name"] = customer.Name,
    ["Email"] = customer.Email,
    ["City"] = customer.City,
    ["CreatedAt"] = customer.CreatedAt.ToString("O")
});

var dialect = new GenericSqlDialect();
var ingestor = new BatchIngestor<CustomerRecord>(
    connectionFactory,
    dialect,
    mapper,
    options);

Console.WriteLine($"\nGenerating 1,000,000 sample records...");
var totalRecords = 1_000_000;

var data = SampleData.GenerateCustomersAsync(totalRecords);

Console.WriteLine($"Starting batch ingestion of {totalRecords:N0} records...\n");

try
{
    var metrics = await ingestor.IngestAsync(data, "Customers", CancellationToken.None);

    Console.WriteLine("\n=== Ingestion Complete ===");
    Console.WriteLine($"Total Rows:      {metrics.TotalRowsProcessed:N0}");
    Console.WriteLine($"Total Batches:   {metrics.BatchesCompleted:N0}");
    Console.WriteLine($"Elapsed Time:    {metrics.ElapsedTime}");
    Console.WriteLine($"Throughput:      {metrics.RowsPerSecond:N0} rows/sec");
    Console.WriteLine($"Avg Batch Time:  {metrics.AverageBatchDuration.TotalMilliseconds:N2}ms");
    Console.WriteLine($"Min Batch Time:  {metrics.MinBatchDuration.TotalMilliseconds:N2}ms");
    Console.WriteLine($"Max Batch Time:  {metrics.MaxBatchDuration.TotalMilliseconds:N2}ms");
    Console.WriteLine($"Errors:          {metrics.ErrorCount}");
    Console.WriteLine($"Retries:         {metrics.RetryCount}");
}
catch (Exception ex)
{
    logger.LogError(ex, "Ingestion failed");
    return 1;
}

await using (var verifyConn = await connectionFactory.CreateConnectionAsync())
{
    using var cmd = verifyConn.CreateCommand();
    cmd.CommandText = "SELECT COUNT(*) FROM Customers";
    var count = (long)(await cmd.ExecuteScalarAsync() ?? 0L);
    Console.WriteLine($"\nVerification: {count:N0} rows in database");
}

Console.WriteLine("\n=== Sample Complete ===");
return 0;
