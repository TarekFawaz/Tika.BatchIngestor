using Microsoft.AspNetCore.Mvc;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.DemoApi.Configuration;
using Tika.BatchIngestor.DemoApi.Models;
using Tika.BatchIngestor.Extensions.DependencyInjection;

namespace Tika.BatchIngestor.DemoApi.Controllers;

/// <summary>
/// Controller for SQL Server batch ingestion operations.
/// Demonstrates how to use Tika.BatchIngestor with SQL Server.
/// </summary>
[ApiController]
[Route("api/sqlserver")]
[Produces("application/json")]
public class SqlServerIngestionController : ControllerBase
{
    private readonly IBatchIngestorFactory _factory;
    private readonly DatabaseSettings _settings;
    private readonly ILogger<SqlServerIngestionController> _logger;

    public SqlServerIngestionController(
        IBatchIngestorFactory factory,
        DatabaseSettings settings,
        ILogger<SqlServerIngestionController> logger)
    {
        _factory = factory;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Ingest sensor readings into SQL Server.
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
    /// Ingest customer records into SQL Server.
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
    /// Ingest order records into SQL Server.
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
    /// Generate and ingest sample sensor data for testing.
    /// </summary>
    /// <param name="tableName">Target table name.</param>
    /// <param name="count">Number of records to generate (default: 10000).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Ingestion metrics.</returns>
    [HttpPost("sensors/generate")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<BatchIngestResponse>> GenerateAndIngestSensorsAsync(
        [FromQuery] string tableName = "SensorReadings",
        [FromQuery] int count = 10000,
        CancellationToken cancellationToken = default)
    {
        var data = GenerateSensorData(count);
        return await IngestDataAsync(
            tableName,
            data,
            new SensorReadingRowMapper(),
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

        if (string.IsNullOrWhiteSpace(_settings.SqlServerConnectionString))
        {
            return StatusCode(500, new BatchIngestResponse
            {
                Success = false,
                ErrorMessage = "SQL Server connection string is not configured."
            });
        }

        try
        {
            var options = CreateBatchIngestOptions();
            var ingestor = _factory.CreateSqlServerIngestor(
                _settings.SqlServerConnectionString,
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
            _logger.LogError(ex, "Error ingesting data to SQL Server table {TableName}", tableName);

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

    private static List<SensorReading> GenerateSensorData(int count)
    {
        var random = new Random(42);
        var sensorTypes = new[] { "Temperature", "Humidity", "Pressure", "CO2", "Light" };
        var units = new[] { "C", "%", "hPa", "ppm", "lux" };
        var locations = new[] { "Building A", "Building B", "Warehouse", "Office", "Lab" };

        var data = new List<SensorReading>(count);
        for (int i = 1; i <= count; i++)
        {
            var typeIndex = random.Next(sensorTypes.Length);
            data.Add(new SensorReading
            {
                Id = i,
                DeviceId = $"DEVICE-{random.Next(1, 100):D3}",
                SensorType = sensorTypes[typeIndex],
                Value = Math.Round(random.NextDouble() * 100, 2),
                Unit = units[typeIndex],
                Timestamp = DateTime.UtcNow.AddMinutes(-random.Next(0, 1440)),
                Location = locations[random.Next(locations.Length)]
            });
        }
        return data;
    }
}
