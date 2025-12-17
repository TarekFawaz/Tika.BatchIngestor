using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using Microsoft.Data.Sqlite;
using Tika.BatchIngestor;
using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Dialects;
using Tika.BatchIngestor.Factories;

namespace Tika.BatchIngestor.Benchmarks;

[Config(typeof(Config))]
[MemoryDiagnoser]
[ThreadingDiagnoser]
public class IoTBenchmarks
{
    private class Config : ManualConfig
    {
        public Config()
        {
            AddJob(Job.Default
                .WithWarmupCount(1)
                .WithIterationCount(3)
                .WithInvocationCount(1)
                .WithUnrollFactor(1));

            AddColumn(StatisticColumn.Mean);
            AddColumn(StatisticColumn.StdDev);
            AddColumn(StatisticColumn.Min);
            AddColumn(StatisticColumn.Max);
            AddColumn(StatisticColumn.Median);
            AddColumn(StatisticColumn.P95);

            AddExporter(MarkdownExporter.GitHub);
            AddExporter(HtmlExporter.Default);
            AddLogger(ConsoleLogger.Default);
        }
    }

    private string _connectionString = string.Empty;
    private SqliteConnection? _connection;

    [Params(5000, 10000, 15000)]
    public int RowCount { get; set; }

    [Params(500, 1000, 2000)]
    public int BatchSize { get; set; }

    [Params(2, 4, 8)]
    public int MaxDegreeOfParallelism { get; set; }

    [GlobalSetup]
    public void GlobalSetup()
    {
        _connectionString = $"Data Source=benchmark_{Guid.NewGuid()};Mode=Memory;Cache=Shared";
        _connection = new SqliteConnection(_connectionString);
        _connection.Open();

        SetupSensorReadingsTable();
        SetupVehicleTelemetryTable();
        SetupTimeSeriesMetricsTable();
        SetupIndustrialMachineLogTable();
    }

    [GlobalCleanup]
    public void GlobalCleanup()
    {
        _connection?.Close();
        _connection?.Dispose();
    }

    private void SetupSensorReadingsTable()
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS SensorReadings (
                DeviceId TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                Temperature REAL,
                Humidity REAL,
                Pressure REAL,
                SensorType TEXT,
                BatteryLevel INTEGER
            )";
        cmd.ExecuteNonQuery();
    }

    private void SetupVehicleTelemetryTable()
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS VehicleTelemetry (
                VehicleId TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                Latitude REAL,
                Longitude REAL,
                Speed REAL,
                FuelLevel REAL,
                EngineTemp REAL,
                OilPressure REAL,
                Rpm INTEGER,
                Odometer INTEGER,
                DriverId TEXT,
                Status TEXT,
                EngineOn INTEGER
            )";
        cmd.ExecuteNonQuery();
    }

    private void SetupTimeSeriesMetricsTable()
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS TimeSeriesMetrics (
                MetricName TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                Value REAL,
                Tags TEXT
            )";
        cmd.ExecuteNonQuery();
    }

    private void SetupIndustrialMachineLogTable()
    {
        using var cmd = _connection!.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS IndustrialMachineLogs (
                MachineId TEXT NOT NULL,
                Timestamp TEXT NOT NULL,
                EventType TEXT,
                ErrorCode INTEGER,
                ErrorMessage TEXT,
                Temperature REAL,
                Vibration REAL,
                PowerConsumption REAL,
                ProductionCount INTEGER,
                DefectCount INTEGER,
                Operator TEXT,
                Location TEXT,
                DiagnosticData TEXT,
                MaintenanceNotes TEXT
            )";
        cmd.ExecuteNonQuery();
    }

    [Benchmark(Description = "Small IoT Sensor Readings (~100 bytes/row)")]
    public async Task<BatchIngestMetrics> IngestSensorReadings()
    {
        var data = GenerateSensorReadings(RowCount);
        return await IngestData(data, "SensorReadings", MapSensorReading);
    }

    [Benchmark(Description = "Medium Vehicle Telemetry (~500 bytes/row)")]
    public async Task<BatchIngestMetrics> IngestVehicleTelemetry()
    {
        var data = GenerateVehicleTelemetry(RowCount);
        return await IngestData(data, "VehicleTelemetry", MapVehicleTelemetry);
    }

    [Benchmark(Description = "Minimal Time-Series Metrics (~64 bytes/row)")]
    public async Task<BatchIngestMetrics> IngestTimeSeriesMetrics()
    {
        var data = GenerateTimeSeriesMetrics(RowCount);
        return await IngestData(data, "TimeSeriesMetrics", MapTimeSeriesMetric);
    }

    [Benchmark(Description = "Large Industrial Machine Logs (~2KB/row)")]
    public async Task<BatchIngestMetrics> IngestIndustrialMachineLogs()
    {
        var data = GenerateIndustrialMachineLogs(RowCount);
        return await IngestData(data, "IndustrialMachineLogs", MapIndustrialMachineLog);
    }

    private async Task<BatchIngestMetrics> IngestData<T>(
        IEnumerable<T> data,
        string tableName,
        Func<T, Dictionary<string, object?>> mapper)
    {
        var factory = new SimpleConnectionFactory(_connectionString, () => new SqliteConnection(_connectionString));
        var dialect = new GenericSqlDialect();
        var rowMapper = new DefaultRowMapper<T>(mapper);

        var options = new BatchIngestOptions
        {
            BatchSize = BatchSize,
            MaxDegreeOfParallelism = MaxDegreeOfParallelism,
            MaxInFlightBatches = 10,
            UseTransactions = true,
            TransactionPerBatch = true,
            EnableCpuThrottling = false, // Disabled for benchmarking
            EnablePerformanceMetrics = true,
            RetryPolicy = null // No retries in benchmarks
        };

        var ingestor = new BatchIngestor<T>(factory, dialect, rowMapper, options);
        return await ingestor.IngestAsync(data, tableName);
    }

    private static List<SensorReading> GenerateSensorReadings(int count)
    {
        var readings = new List<SensorReading>(count);
        var random = new Random(42); // Fixed seed for reproducibility
        var baseTime = DateTime.UtcNow.AddDays(-1);

        for (int i = 0; i < count; i++)
        {
            readings.Add(new SensorReading
            {
                DeviceId = Guid.NewGuid(),
                Timestamp = baseTime.AddSeconds(i),
                Temperature = 20 + random.NextDouble() * 15,
                Humidity = 30 + random.NextDouble() * 40,
                Pressure = 980 + random.NextDouble() * 40,
                SensorType = $"Type{random.Next(1, 6)}",
                BatteryLevel = random.Next(0, 101)
            });
        }

        return readings;
    }

    private static List<VehicleTelemetry> GenerateVehicleTelemetry(int count)
    {
        var telemetry = new List<VehicleTelemetry>(count);
        var random = new Random(42);
        var baseTime = DateTime.UtcNow.AddDays(-1);

        for (int i = 0; i < count; i++)
        {
            telemetry.Add(new VehicleTelemetry
            {
                VehicleId = $"VH{random.Next(1000, 10000)}",
                Timestamp = baseTime.AddSeconds(i),
                Latitude = 40.7128 + random.NextDouble() * 0.1,
                Longitude = -74.0060 + random.NextDouble() * 0.1,
                Speed = random.NextDouble() * 120,
                FuelLevel = random.NextDouble() * 100,
                EngineTemp = 80 + random.NextDouble() * 40,
                OilPressure = 20 + random.NextDouble() * 60,
                Rpm = random.Next(800, 6000),
                Odometer = random.Next(1000, 200000),
                DriverId = $"DR{random.Next(100, 1000)}",
                Status = random.Next(0, 2) == 0 ? "Active" : "Idle",
                EngineOn = random.Next(0, 2) == 1
            });
        }

        return telemetry;
    }

    private static List<TimeSeriesMetric> GenerateTimeSeriesMetrics(int count)
    {
        var metrics = new List<TimeSeriesMetric>(count);
        var random = new Random(42);
        var baseTime = DateTime.UtcNow.AddDays(-1);

        for (int i = 0; i < count; i++)
        {
            metrics.Add(new TimeSeriesMetric
            {
                MetricName = $"metric_{random.Next(1, 20)}",
                Timestamp = baseTime.AddSeconds(i),
                Value = random.NextDouble() * 1000,
                Tags = $"env=prod,region=us-east-{random.Next(1, 4)}"
            });
        }

        return metrics;
    }

    private static List<IndustrialMachineLog> GenerateIndustrialMachineLogs(int count)
    {
        var logs = new List<IndustrialMachineLog>(count);
        var random = new Random(42);
        var baseTime = DateTime.UtcNow.AddDays(-1);

        for (int i = 0; i < count; i++)
        {
            logs.Add(new IndustrialMachineLog
            {
                MachineId = Guid.NewGuid(),
                Timestamp = baseTime.AddSeconds(i),
                EventType = random.Next(0, 10) == 0 ? "Error" : "Info",
                ErrorCode = random.Next(0, 1000),
                ErrorMessage = random.Next(0, 10) == 0 ? "Critical failure detected" : "Normal operation",
                Temperature = 60 + random.NextDouble() * 40,
                Vibration = random.NextDouble() * 10,
                PowerConsumption = 100 + random.NextDouble() * 500,
                ProductionCount = random.Next(0, 1000),
                DefectCount = random.Next(0, 10),
                Operator = $"OP{random.Next(1, 50)}",
                Location = $"Floor{random.Next(1, 5)}-Line{random.Next(1, 10)}",
                DiagnosticData = GenerateJsonDiagnostics(random),
                MaintenanceNotes = "Routine inspection completed"
            });
        }

        return logs;
    }

    private static string GenerateJsonDiagnostics(Random random)
    {
        return $@"{{
            ""cpuUsage"": {random.NextDouble() * 100:F2},
            ""memoryUsage"": {random.NextDouble() * 100:F2},
            ""diskIO"": {random.Next(0, 1000)},
            ""networkTraffic"": {random.Next(0, 10000)},
            ""sensorReadings"": [{random.NextDouble():F2}, {random.NextDouble():F2}, {random.NextDouble():F2}]
        }}";
    }

    private static Dictionary<string, object?> MapSensorReading(SensorReading sr) => new()
    {
        ["DeviceId"] = sr.DeviceId.ToString(),
        ["Timestamp"] = sr.Timestamp.ToString("O"),
        ["Temperature"] = sr.Temperature,
        ["Humidity"] = sr.Humidity,
        ["Pressure"] = sr.Pressure,
        ["SensorType"] = sr.SensorType,
        ["BatteryLevel"] = sr.BatteryLevel
    };

    private static Dictionary<string, object?> MapVehicleTelemetry(VehicleTelemetry vt) => new()
    {
        ["VehicleId"] = vt.VehicleId,
        ["Timestamp"] = vt.Timestamp.ToString("O"),
        ["Latitude"] = vt.Latitude,
        ["Longitude"] = vt.Longitude,
        ["Speed"] = vt.Speed,
        ["FuelLevel"] = vt.FuelLevel,
        ["EngineTemp"] = vt.EngineTemp,
        ["OilPressure"] = vt.OilPressure,
        ["Rpm"] = vt.Rpm,
        ["Odometer"] = vt.Odometer,
        ["DriverId"] = vt.DriverId,
        ["Status"] = vt.Status,
        ["EngineOn"] = vt.EngineOn ? 1 : 0
    };

    private static Dictionary<string, object?> MapTimeSeriesMetric(TimeSeriesMetric tsm) => new()
    {
        ["MetricName"] = tsm.MetricName,
        ["Timestamp"] = tsm.Timestamp.ToString("O"),
        ["Value"] = tsm.Value,
        ["Tags"] = tsm.Tags
    };

    private static Dictionary<string, object?> MapIndustrialMachineLog(IndustrialMachineLog iml) => new()
    {
        ["MachineId"] = iml.MachineId.ToString(),
        ["Timestamp"] = iml.Timestamp.ToString("O"),
        ["EventType"] = iml.EventType,
        ["ErrorCode"] = iml.ErrorCode,
        ["ErrorMessage"] = iml.ErrorMessage,
        ["Temperature"] = iml.Temperature,
        ["Vibration"] = iml.Vibration,
        ["PowerConsumption"] = iml.PowerConsumption,
        ["ProductionCount"] = iml.ProductionCount,
        ["DefectCount"] = iml.DefectCount,
        ["Operator"] = iml.Operator,
        ["Location"] = iml.Location,
        ["DiagnosticData"] = iml.DiagnosticData,
        ["MaintenanceNotes"] = iml.MaintenanceNotes
    };
}
