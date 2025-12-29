using Microsoft.AspNetCore.Mvc;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.DemoApi.Configuration;
using Tika.BatchIngestor.DemoApi.Models;
using Tika.BatchIngestor.Extensions.DependencyInjection;

namespace Tika.BatchIngestor.DemoApi.Controllers;

/// <summary>
/// Controller for PostgreSQL batch ingestion operations.
/// Demonstrates how to use Tika.BatchIngestor with PostgreSQL.
/// </summary>
[ApiController]
[Route("api/postgresql")]
[Produces("application/json")]
public class PostgreSqlIngestionController : ControllerBase
{
    private readonly IBatchIngestorFactory _factory;
    private readonly DatabaseSettings _settings;
    private readonly ILogger<PostgreSqlIngestionController> _logger;

    public PostgreSqlIngestionController(
        IBatchIngestorFactory factory,
        DatabaseSettings settings,
        ILogger<PostgreSqlIngestionController> logger)
    {
        _factory = factory;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Ingest sensor readings into PostgreSQL.
    /// </summary>
    /// <param name="request">Batch ingest request containing table name and data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ingestion metrics.</returns>
    [HttpPost("sensors")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BatchIngestResponse>> IngestSensorsAsync(
        [FromBody] BatchIngestRequest<SensorReading> request,
        CancellationToken cancellationToken)
    {
        return await IngestDataAsync(
            request.TableName,
            request.Data,
            new SensorReadingRowMapper(),
            cancellationToken);
    }

    /// <summary>
    /// Ingest customer records into PostgreSQL.
    /// </summary>
    /// <param name="request">Batch ingest request containing table name and data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ingestion metrics.</returns>
    [HttpPost("customers")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BatchIngestResponse>> IngestCustomersAsync(
        [FromBody] BatchIngestRequest<CustomerRecord> request,
        CancellationToken cancellationToken)
    {
        return await IngestDataAsync(
            request.TableName,
            request.Data,
            new CustomerRecordRowMapper(),
            cancellationToken);
    }

    /// <summary>
    /// Ingest order records into PostgreSQL.
    /// </summary>
    /// <param name="request">Batch ingest request containing table name and data.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ingestion metrics.</returns>
    [HttpPost("orders")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BatchIngestResponse>> IngestOrdersAsync(
        [FromBody] BatchIngestRequest<OrderRecord> request,
        CancellationToken cancellationToken)
    {
        return await IngestDataAsync(
            request.TableName,
            request.Data,
            new OrderRecordRowMapper(),
            cancellationToken);
    }

    /// <summary>
    /// Generate and ingest sample customer data for testing.
    /// </summary>
    /// <param name="tableName">Target table name.</param>
    /// <param name="count">Number of records to generate (default: 10000).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ingestion metrics.</returns>
    [HttpPost("customers/generate")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BatchIngestResponse>> GenerateAndIngestCustomersAsync(
        [FromQuery] string tableName = "customers",
        [FromQuery] int count = 10000,
        CancellationToken cancellationToken = default)
    {
        var data = GenerateCustomerData(count);
        return await IngestDataAsync(
            tableName,
            data,
            new CustomerRecordRowMapper(),
            cancellationToken);
    }

    private async Task<ActionResult<BatchIngestResponse>> IngestDataAsync<T>(
        string tableName,
        IEnumerable<T> data,
        IRowMapper<T> mapper,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tableName))
        {
            return BadRequest(new BatchIngestResponse
            {
                Success = false,
                ErrorMessage = "Table name is required."
            });
        }

        if (string.IsNullOrWhiteSpace(_settings.PostgreSqlConnectionString))
        {
            return StatusCode(500, new BatchIngestResponse
            {
                Success = false,
                ErrorMessage = "PostgreSQL connection string is not configured."
            });
        }

        try
        {
            var options = CreateBatchIngestOptions();
            var ingestor = _factory.CreatePostgreSqlIngestor(
                _settings.PostgreSqlConnectionString,
                mapper,
                options);

            var metrics = await ingestor.IngestAsync(data, tableName, cancellationToken);

            return Ok(new BatchIngestResponse
            {
                Success = true,
                TotalRowsProcessed = metrics.TotalRowsProcessed,
                BatchesCompleted = metrics.BatchesCompleted,
                ElapsedSeconds = metrics.ElapsedTime.TotalSeconds,
                RowsPerSecond = metrics.RowsPerSecond,
                ErrorCount = metrics.ErrorCount,
                RetryCount = metrics.RetryCount
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error ingesting data to PostgreSQL table {TableName}", tableName);

            return StatusCode(500, new BatchIngestResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
    }

    private BatchIngestOptions CreateBatchIngestOptions()
    {
        return new BatchIngestOptions
        {
            BatchSize = _settings.DefaultBatchSize,
            MaxDegreeOfParallelism = _settings.MaxDegreeOfParallelism,
            EnableCpuThrottling = _settings.EnableCpuThrottling,
            MaxCpuPercent = _settings.MaxCpuPercent,
            UseTransactions = true,
            TransactionPerBatch = true,
            EnablePerformanceMetrics = true,
            RetryPolicy = new RetryPolicy
            {
                MaxRetries = 3,
                UseExponentialBackoff = true,
                UseJitter = true
            }
        };
    }

    private static List<CustomerRecord> GenerateCustomerData(int count)
    {
        var random = new Random(42);
        var firstNames = new[] { "Alice", "Bob", "Charlie", "Diana", "Eve", "Frank", "Grace", "Henry" };
        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller" };
        var cities = new[] { "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Seattle" };

        var data = new List<CustomerRecord>(count);
        for (int i = 1; i <= count; i++)
        {
            var firstName = firstNames[random.Next(firstNames.Length)];
            var lastName = lastNames[random.Next(lastNames.Length)];

            data.Add(new CustomerRecord
            {
                Id = i,
                Name = $"{firstName} {lastName}",
                Email = $"{firstName.ToLower()}.{lastName.ToLower()}{i}@example.com",
                City = cities[random.Next(cities.Length)],
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 365))
            });
        }
        return data;
    }
}
