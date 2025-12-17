namespace Tika.BatchIngestor.Benchmarks;

/// <summary>
/// Represents an IoT sensor reading (small payload ~100 bytes).
/// </summary>
public class SensorReading
{
    public Guid DeviceId { get; set; }
    public DateTime Timestamp { get; set; }
    public double Temperature { get; set; }
    public double Humidity { get; set; }
    public double Pressure { get; set; }
    public string SensorType { get; set; } = string.Empty;
    public int BatteryLevel { get; set; }
}

/// <summary>
/// Represents a vehicle telemetry event (medium payload ~500 bytes).
/// </summary>
public class VehicleTelemetry
{
    public string VehicleId { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Speed { get; set; }
    public double FuelLevel { get; set; }
    public double EngineTemp { get; set; }
    public double OilPressure { get; set; }
    public int Rpm { get; set; }
    public int Odometer { get; set; }
    public string DriverId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool EngineOn { get; set; }
}

/// <summary>
/// Represents a time-series metric (minimal payload ~64 bytes).
/// </summary>
public class TimeSeriesMetric
{
    public string MetricName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public double Value { get; set; }
    public string Tags { get; set; } = string.Empty;
}

/// <summary>
/// Represents an industrial machine log (large payload ~2KB).
/// </summary>
public class IndustrialMachineLog
{
    public Guid MachineId { get; set; }
    public DateTime Timestamp { get; set; }
    public string EventType { get; set; } = string.Empty;
    public int ErrorCode { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public double Temperature { get; set; }
    public double Vibration { get; set; }
    public double PowerConsumption { get; set; }
    public int ProductionCount { get; set; }
    public int DefectCount { get; set; }
    public string Operator { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string DiagnosticData { get; set; } = string.Empty; // Large JSON payload
    public string MaintenanceNotes { get; set; } = string.Empty;
}
