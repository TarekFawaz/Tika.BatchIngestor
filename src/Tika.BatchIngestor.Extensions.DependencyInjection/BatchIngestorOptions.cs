namespace Tika.BatchIngestor.Extensions.DependencyInjection;

/// <summary>
/// Batch ingestor settings for configuration via appsettings.json.
/// Supports multiple database connections with different dialects.
/// </summary>
public class BatchIngestorSettings
{
    /// <summary>
    /// Configuration section name in appsettings.json.
    /// </summary>
    public const string SectionName = "BatchIngestor";

    /// <summary>
    /// List of database connections to configure.
    /// </summary>
    public List<DatabaseConnection> Connections { get; set; } = new();

    /// <summary>
    /// Default batch size for all ingestors. Can be overridden per connection.
    /// </summary>
    public int DefaultBatchSize { get; set; } = 1000;

    /// <summary>
    /// Default maximum degree of parallelism. Can be overridden per connection.
    /// </summary>
    public int DefaultMaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Enable CPU throttling by default.
    /// </summary>
    public bool EnableCpuThrottling { get; set; } = true;

    /// <summary>
    /// Default maximum CPU percentage before throttling.
    /// </summary>
    public double MaxCpuPercent { get; set; } = 80.0;

    /// <summary>
    /// Default throttle delay in milliseconds.
    /// </summary>
    public int ThrottleDelayMs { get; set; } = 100;

    /// <summary>
    /// Enable performance metrics collection by default.
    /// </summary>
    public bool EnablePerformanceMetrics { get; set; } = true;

    /// <summary>
    /// Default retry policy settings.
    /// </summary>
    public RetryPolicySettings RetryPolicy { get; set; } = new();
}

/// <summary>
/// Represents a database connection configuration.
/// </summary>
public class DatabaseConnection
{
    /// <summary>
    /// Unique name for this connection (used for keyed DI registration).
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The dialect type to use. Supported values:
    /// SqlServer, PostgreSql, AuroraPostgreSql, AuroraMySql, AzureSql, Generic
    /// </summary>
    public string Dialect { get; set; } = "Generic";

    /// <summary>
    /// The database connection string.
    /// </summary>
    public string ConnectionString { get; set; } = string.Empty;

    /// <summary>
    /// Optional: Override the default batch size for this connection.
    /// </summary>
    public int? BatchSize { get; set; }

    /// <summary>
    /// Optional: Override the default max parallelism for this connection.
    /// </summary>
    public int? MaxDegreeOfParallelism { get; set; }

    /// <summary>
    /// Optional: Enable/disable this connection.
    /// </summary>
    public bool Enabled { get; set; } = true;
}

/// <summary>
/// Retry policy settings for configuration.
/// </summary>
public class RetryPolicySettings
{
    /// <summary>
    /// Maximum number of retry attempts.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before first retry.
    /// </summary>
    public int InitialDelayMs { get; set; } = 100;

    /// <summary>
    /// Maximum delay in milliseconds between retries.
    /// </summary>
    public int MaxDelayMs { get; set; } = 5000;

    /// <summary>
    /// Use exponential backoff for retry delays.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Add jitter to retry delays to prevent thundering herd.
    /// </summary>
    public bool UseJitter { get; set; } = true;
}

/// <summary>
/// Supported database dialects as string constants.
/// Using strings instead of enum for better NuGet flexibility.
/// </summary>
public static class DialectTypes
{
    public const string SqlServer = "SqlServer";
    public const string PostgreSql = "PostgreSql";
    public const string AuroraPostgreSql = "AuroraPostgreSql";
    public const string AuroraMySql = "AuroraMySql";
    public const string AzureSql = "AzureSql";
    public const string Generic = "Generic";

    /// <summary>
    /// Gets all supported dialect types.
    /// </summary>
    public static IReadOnlyList<string> All => new[]
    {
        SqlServer, PostgreSql, AuroraPostgreSql, AuroraMySql, AzureSql, Generic
    };

    /// <summary>
    /// Checks if a dialect type is valid.
    /// </summary>
    public static bool IsValid(string dialect)
    {
        return All.Contains(dialect, StringComparer.OrdinalIgnoreCase);
    }
}
