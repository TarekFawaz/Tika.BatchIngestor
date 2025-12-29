namespace Tika.BatchIngestor.DemoApi.Models;

/// <summary>
/// Represents an IoT sensor reading for batch ingestion.
/// </summary>
public class SensorReading
{
    public long Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string SensorType { get; set; } = string.Empty;
    public double Value { get; set; }
    public string? Unit { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Location { get; set; }
}

/// <summary>
/// Request model for batch ingestion.
/// </summary>
public class BatchIngestRequest<T>
{
    /// <summary>
    /// The target database table name.
    /// </summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>
    /// The data to ingest.
    /// </summary>
    public List<T> Data { get; set; } = new();
}

/// <summary>
/// Response model for batch ingestion results.
/// </summary>
public class BatchIngestResponse
{
    public bool Success { get; set; }
    public long TotalRowsProcessed { get; set; }
    public int BatchesCompleted { get; set; }
    public double ElapsedSeconds { get; set; }
    public double RowsPerSecond { get; set; }
    public int ErrorCount { get; set; }
    public int RetryCount { get; set; }
    public string? ErrorMessage { get; set; }
}
