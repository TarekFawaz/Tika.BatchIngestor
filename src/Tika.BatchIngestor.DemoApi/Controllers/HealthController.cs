using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Tika.BatchIngestor.DemoApi.Controllers;

/// <summary>
/// Controller for health check endpoints.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class HealthController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    /// <summary>
    /// Get the current health status of the API and all registered health checks.
    /// </summary>
    /// <returns>Health check results.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(HealthReportResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthReportResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<ActionResult<HealthReportResponse>> GetHealthAsync(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

        var response = new HealthReportResponse
        {
            Status = report.Status.ToString(),
            TotalDuration = report.TotalDuration.TotalMilliseconds,
            Entries = report.Entries.Select(e => new HealthEntryResponse
            {
                Name = e.Key,
                Status = e.Value.Status.ToString(),
                Description = e.Value.Description,
                Duration = e.Value.Duration.TotalMilliseconds,
                Exception = e.Value.Exception?.Message,
                Data = e.Value.Data.ToDictionary(d => d.Key, d => d.Value?.ToString())
            }).ToList()
        };

        return report.Status == HealthStatus.Healthy
            ? Ok(response)
            : StatusCode(503, response);
    }

    /// <summary>
    /// Simple liveness probe endpoint.
    /// </summary>
    /// <returns>OK if the service is alive.</returns>
    [HttpGet("live")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetLiveness()
    {
        return Ok(new { status = "alive", timestamp = DateTime.UtcNow });
    }

    /// <summary>
    /// Readiness probe endpoint.
    /// </summary>
    /// <returns>OK if the service is ready to accept requests.</returns>
    [HttpGet("ready")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetReadiness(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken);

        return report.Status == HealthStatus.Healthy
            ? Ok(new { status = "ready", timestamp = DateTime.UtcNow })
            : StatusCode(503, new { status = "not ready", timestamp = DateTime.UtcNow });
    }
}

/// <summary>
/// Health report response model.
/// </summary>
public class HealthReportResponse
{
    public string Status { get; set; } = string.Empty;
    public double TotalDuration { get; set; }
    public List<HealthEntryResponse> Entries { get; set; } = new();
}

/// <summary>
/// Individual health check entry response.
/// </summary>
public class HealthEntryResponse
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? Description { get; set; }
    public double Duration { get; set; }
    public string? Exception { get; set; }
    public Dictionary<string, string?> Data { get; set; } = new();
}
