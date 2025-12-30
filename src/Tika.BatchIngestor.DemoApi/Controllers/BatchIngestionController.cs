using Microsoft.AspNetCore.Mvc;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.DemoApi.Configuration;
using Tika.BatchIngestor.DemoApi.Models;
using Tika.BatchIngestor.Extensions.DependencyInjection;

namespace Tika.BatchIngestor.DemoApi.Controllers;

/// <summary>
/// Unified controller for batch ingestion operations across all supported dialects.
/// Supports: SqlServer, PostgreSql, AuroraPostgreSql, AuroraMySql, AzureSql
/// </summary>
[ApiController]
[Route("api/ingest")]
[Produces("application/json")]
public class BatchIngestionController : ControllerBase
{
    private readonly IBatchIngestorFactory _factory;
    private readonly BatchIngestorSettings _settings;
    private readonly ILogger<BatchIngestionController> _logger;

    public BatchIngestionController(
        IBatchIngestorFactory factory,
        BatchIngestorSettings settings,
        ILogger<BatchIngestionController> logger)
    {
        _factory = factory;
        _settings = settings;
        _logger = logger;
    }

    /// <summary>
    /// Get all configured database connections.
    /// </summary>
    [HttpGet("connections")]
    [ProducesResponseType(typeof(IEnumerable<ConnectionInfo>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<ConnectionInfo>> GetConnections()
    {
        var connections = _settings.Connections
            .Where(c => c.Enabled)
            .Select(c => new ConnectionInfo
            {
                Name = c.Name,
                Dialect = c.Dialect,
                IsConfigured = !string.IsNullOrEmpty(c.ConnectionString)
            });

        return Ok(connections);
    }

    /// <summary>
    /// Get all supported dialect types.
    /// </summary>
    [HttpGet("dialects")]
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    public ActionResult<IEnumerable<string>> GetDialects()
    {
        return Ok(DialectTypes.All);
    }

    /// <summary>
    /// Ingest sensor readings using a named connection from configuration.
    /// </summary>
    [HttpPost("{connectionName}/sensors")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchIngestResponse>> IngestSensorsByConnectionAsync(
        string connectionName,
        [FromBody] BatchIngestRequest<SensorReading> request,
        CancellationToken cancellationToken)
    {
        var connection = GetConnection(connectionName);
        if (connection == null)
            return NotFound(CreateNotFoundResponse(connectionName));

        return await IngestDataAsync(
            connection.Dialect,
            connection.ConnectionString,
            request.TableName,
            request.Data,
            new SensorReadingRowMapper(),
            cancellationToken);
    }

    /// <summary>
    /// Ingest customer records using a named connection from configuration.
    /// </summary>
    [HttpPost("{connectionName}/customers")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchIngestResponse>> IngestCustomersByConnectionAsync(
        string connectionName,
        [FromBody] BatchIngestRequest<CustomerRecord> request,
        CancellationToken cancellationToken)
    {
        var connection = GetConnection(connectionName);
        if (connection == null)
            return NotFound(CreateNotFoundResponse(connectionName));

        return await IngestDataAsync(
            connection.Dialect,
            connection.ConnectionString,
            request.TableName,
            request.Data,
            new CustomerRecordRowMapper(),
            cancellationToken);
    }

    /// <summary>
    /// Ingest order records using a named connection from configuration.
    /// </summary>
    [HttpPost("{connectionName}/orders")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchIngestResponse>> IngestOrdersByConnectionAsync(
        string connectionName,
        [FromBody] BatchIngestRequest<OrderRecord> request,
        CancellationToken cancellationToken)
    {
        var connection = GetConnection(connectionName);
        if (connection == null)
            return NotFound(CreateNotFoundResponse(connectionName));

        return await IngestDataAsync(
            connection.Dialect,
            connection.ConnectionString,
            request.TableName,
            request.Data,
            new OrderRecordRowMapper(),
            cancellationToken);
    }

    /// <summary>
    /// Generate and ingest sample sensor data using a named connection.
    /// </summary>
    [HttpPost("{connectionName}/sensors/generate")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<BatchIngestResponse>> GenerateSensorsByConnectionAsync(
        string connectionName,
        [FromQuery] string tableName = "SensorReadings",
        [FromQuery] int count = 10000,
        CancellationToken cancellationToken = default)
    {
        var connection = GetConnection(connectionName);
        if (connection == null)
            return NotFound(CreateNotFoundResponse(connectionName));

        var data = GenerateSensorData(count);
        return await IngestDataAsync(
            connection.Dialect,
            connection.ConnectionString,
            tableName,
            data,
            new SensorReadingRowMapper(),
            cancellationToken);
    }

    // ===== Direct dialect endpoints =====

    /// <summary>
    /// Ingest sensors directly specifying dialect and connection string.
    /// </summary>
    [HttpPost("direct/sensors")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchIngestResponse>> IngestSensorsDirectAsync(
        [FromQuery] string dialect,
        [FromQuery] string connectionString,
        [FromBody] BatchIngestRequest<SensorReading> request,
        CancellationToken cancellationToken)
    {
        if (!DialectTypes.IsValid(dialect))
            return BadRequest(new BatchIngestResponse
            {
                Success = false,
                ErrorMessage = $"Invalid dialect '{dialect}'. Supported: {string.Join(", ", DialectTypes.All)}"
            });

        return await IngestDataAsync(
            dialect,
            connectionString,
            request.TableName,
            request.Data,
            new SensorReadingRowMapper(),
            cancellationToken);
    }

    /// <summary>
    /// Ingest customers directly specifying dialect and connection string.
    /// </summary>
    [HttpPost("direct/customers")]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(BatchIngestResponse), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<BatchIngestResponse>> IngestCustomersDirectAsync(
        [FromQuery] string dialect,
        [FromQuery] string connectionString,
        [FromBody] BatchIngestRequest<CustomerRecord> request,
        CancellationToken cancellationToken)
    {
        if (!DialectTypes.IsValid(dialect))
            return BadRequest(new BatchIngestResponse
            {
                Success = false,
                ErrorMessage = $"Invalid dialect '{dialect}'. Supported: {string.Join(", ", DialectTypes.All)}"
            });

        return await IngestDataAsync(
            dialect,
            connectionString,
            request.TableName,
            request.Data,
            new CustomerRecordRowMapper(),
            cancellationToken);
    }

    private DatabaseConnection? GetConnection(string name)
    {
        return _settings.Connections
            .FirstOrDefault(c => c.Name.Equals(name, StringComparison.OrdinalIgnoreCase) && c.Enabled);
    }

    private static BatchIngestResponse CreateNotFoundResponse(string connectionName)
    {
        return new BatchIngestResponse
        {
            Success = false,
            ErrorMessage = $"Connection '{connectionName}' not found or not enabled in configuration."
        };
    }

    private async Task<ActionResult<BatchIngestResponse>> IngestDataAsync<T>(
        string dialect,
        string connectionString,
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

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return BadRequest(new BatchIngestResponse
            {
                Success = false,
                ErrorMessage = "Connection string is not configured."
            });
        }

        try
        {
            var ingestor = _factory.CreateIngestor(dialect, connectionString, mapper);
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
            _logger.LogError(ex, "Error ingesting data to {Dialect} table {TableName}", dialect, tableName);

            return StatusCode(500, new BatchIngestResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            });
        }
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

/// <summary>
/// Connection info response model.
/// </summary>
public class ConnectionInfo
{
    public string Name { get; set; } = string.Empty;
    public string Dialect { get; set; } = string.Empty;
    public bool IsConfigured { get; set; }
}
