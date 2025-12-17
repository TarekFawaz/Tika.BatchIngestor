using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using System.Diagnostics;
using Tika.BatchIngestor;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Dialects;
using Tika.BatchIngestor.Factories;
using Tika.BatchIngestor.Samples;
using Tika.BatchIngestor.Samples.Models;

Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
Console.WriteLine("║       Tika.BatchIngestor - Sample Application                  ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
Console.WriteLine();

using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder.AddConsole();
    builder.SetMinimumLevel(LogLevel.Warning); // Only show warnings/errors
});
var logger = loggerFactory.CreateLogger<Program>();

// Use shared cache for in-memory database
var connectionString = "Data Source=InMemorySample;Mode=Memory;Cache=Shared";
var connectionFactory = new SimpleConnectionFactory(
    connectionString,
    () => new SqliteConnection());

// Keep the setup connection open during the entire operation
var setupConn = await connectionFactory.CreateConnectionAsync();
try
{
    // Create table
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
    Console.WriteLine("✓ Database table created");
    Console.WriteLine();

    var totalRecords = 1_000_000;
    Console.WriteLine($" Preparing to ingest {totalRecords:N0} records...");
    Console.WriteLine();

    // Create progress tracker
    var progress = new ProgressBar(totalRecords);

    var options = new BatchIngestOptions
    {
        BatchSize = 5000,
        MaxDegreeOfParallelism = 1,
        MaxInFlightBatches = 5,
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
            progress.Update(
                metrics.TotalRowsProcessed,
                metrics.RowsPerSecond,
                metrics.ElapsedTime
            );
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

    var data = SampleData.GenerateCustomersAsync(totalRecords);

    Console.WriteLine(" Starting ingestion...");
    Console.WriteLine();

    var metrics = await ingestor.IngestAsync(data, "Customers", CancellationToken.None);

    // Final update
    progress.Complete();
    Console.WriteLine();
    Console.WriteLine();

    // Summary
    Console.WriteLine("╔════════════════════════════════════════════════════════════════╗");
    Console.WriteLine("║                    Ingestion Summary                           ║");
    Console.WriteLine("╚════════════════════════════════════════════════════════════════╝");
    Console.WriteLine();
    Console.WriteLine($"  Total Rows:        {metrics.TotalRowsProcessed:N0}");
    Console.WriteLine($"  Total Batches:     {metrics.BatchesCompleted:N0}");
    Console.WriteLine($"  Elapsed Time:      {metrics.ElapsedTime:hh\\:mm\\:ss}");
    Console.WriteLine($"  Throughput:        {metrics.RowsPerSecond:N0} rows/sec");
    Console.WriteLine($"  Avg Batch Time:    {metrics.AverageBatchDuration.TotalMilliseconds:N0} ms");
    Console.WriteLine($"  Min Batch Time:    {metrics.MinBatchDuration.TotalMilliseconds:N0} ms");
    Console.WriteLine($"  Max Batch Time:    {metrics.MaxBatchDuration.TotalMilliseconds:N0} ms");

    if (metrics.ErrorCount > 0 || metrics.RetryCount > 0)
    {
        Console.WriteLine();
        Console.WriteLine($"  Errors:            {metrics.ErrorCount}");
        Console.WriteLine($"  Retries:           {metrics.RetryCount}");
    }

    // Verify data
    Console.WriteLine();
    using var verifyCmd = setupConn.CreateCommand();
    verifyCmd.CommandText = "SELECT COUNT(*) FROM Customers";
    var count = (long)(await verifyCmd.ExecuteScalarAsync() ?? 0L);
    Console.WriteLine($"✓ Verification: {count:N0} rows in database");

    Console.WriteLine();
    Console.WriteLine("════════════════════════════════════════════════════════════════");
    Console.WriteLine(" Sample completed successfully!");
    Console.WriteLine("════════════════════════════════════════════════════════════════");
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine($" Error: {ex.Message}");
    logger.LogError(ex, "Ingestion failed");
    return 1;
}
finally
{
    await setupConn.DisposeAsync();
}

return 0;

// Progress Bar Implementation
public class ProgressBar
{
    private readonly long _total;
    private readonly int _progressBarWidth = 50;
    private readonly object _lock = new();
    private DateTime _startTime = DateTime.Now;
    private int _lastLineCount = 0;

    public ProgressBar(long total)
    {
        _total = total;
        _startTime = DateTime.Now;
    }

    public void Update(long current, double rowsPerSecond, TimeSpan elapsed)
    {
        lock (_lock)
        {
            // Clear previous lines
            if (_lastLineCount > 0)
            {
                Console.SetCursorPosition(0, Console.CursorTop - _lastLineCount);
                for (int i = 0; i < _lastLineCount; i++)
                {
                    Console.Write(new string(' ', Console.WindowWidth - 1));
                    Console.WriteLine();
                }
                Console.SetCursorPosition(0, Console.CursorTop - _lastLineCount);
            }

            var percentage = (double)current / _total * 100;
            var filledWidth = (int)(percentage / 100 * _progressBarWidth);

            // Progress bar
            var progressBar = "[" +
                new string('█', filledWidth) +
                new string('░', _progressBarWidth - filledWidth) +
                "]";

            // Calculate ETA
            var eta = rowsPerSecond > 0
                ? TimeSpan.FromSeconds((_total - current) / rowsPerSecond)
                : TimeSpan.Zero;

            // Build output
            var lines = new List<string>
            {
                $"{progressBar} {percentage:F1}%",
                $"",
                $"  Processed:  {current:N0} / {_total:N0} rows",
                $"  Speed:      {rowsPerSecond:N0} rows/sec",
                $"  Elapsed:    {elapsed:hh\\:mm\\:ss}",
                $"  ETA:        {(eta.TotalSeconds > 0 ? eta.ToString(@"hh\:mm\:ss") : "Calculating...")}"
            };

            // Print all lines
            foreach (var line in lines)
            {
                Console.WriteLine(line);
            }

            _lastLineCount = lines.Count;
        }
    }

    public void Complete()
    {
        lock (_lock)
        {
            // Clear progress
            if (_lastLineCount > 0)
            {
                Console.SetCursorPosition(0, Console.CursorTop - _lastLineCount);
                for (int i = 0; i < _lastLineCount; i++)
                {
                    Console.Write(new string(' ', Console.WindowWidth - 1));
                    Console.WriteLine();
                }
                Console.SetCursorPosition(0, Console.CursorTop - _lastLineCount);
            }

            // Final complete message
            var progressBar = "[" + new string('█', _progressBarWidth) + "]";
            Console.WriteLine($"{progressBar} 100.0%");
            Console.WriteLine();
            Console.WriteLine($"  ✓ Completed: {_total:N0} rows");
        }
    }
}
