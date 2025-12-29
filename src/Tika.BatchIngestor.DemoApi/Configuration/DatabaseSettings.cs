namespace Tika.BatchIngestor.DemoApi.Configuration;

/// <summary>
/// Database connection settings from appsettings.json.
/// </summary>
public class DatabaseSettings
{
    public const string SectionName = "DatabaseSettings";

    /// <summary>
    /// SQL Server connection string.
    /// </summary>
    public string SqlServerConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// PostgreSQL connection string.
    /// </summary>
    public string PostgreSqlConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Default batch size for ingestion.
    /// </summary>
    public int DefaultBatchSize { get; set; } = 1000;

    /// <summary>
    /// Maximum degree of parallelism for ingestion.
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Enable CPU throttling to prevent overloading the system.
    /// </summary>
    public bool EnableCpuThrottling { get; set; } = true;

    /// <summary>
    /// Maximum CPU percentage before throttling kicks in.
    /// </summary>
    public double MaxCpuPercent { get; set; } = 80.0;
}
