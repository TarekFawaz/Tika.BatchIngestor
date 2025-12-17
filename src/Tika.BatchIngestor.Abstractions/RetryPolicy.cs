namespace Tika.BatchIngestor.Abstractions;

/// <summary>
/// Configuration for retry behavior on transient failures.
/// </summary>
public class RetryPolicy
{
    /// <summary>
    /// Maximum number of retry attempts. Default is 3.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay in milliseconds before the first retry. Default is 100ms.
    /// </summary>
    public int InitialDelayMs { get; set; } = 100;

    /// <summary>
    /// Maximum delay in milliseconds between retries. Default is 5000ms (5 seconds).
    /// </summary>
    public int MaxDelayMs { get; set; } = 5000;

    /// <summary>
    /// Whether to use exponential backoff (delays double each retry). Default is true.
    /// </summary>
    public bool UseExponentialBackoff { get; set; } = true;

    /// <summary>
    /// Whether to add random jitter to delays (prevents thundering herd). Default is true.
    /// </summary>
    public bool UseJitter { get; set; } = true;

    /// <summary>
    /// Validates the retry policy.
    /// </summary>
    public void Validate()
    {
        if (MaxRetries < 0)
            throw new ArgumentException("MaxRetries cannot be negative.", nameof(MaxRetries));

        if (InitialDelayMs < 0)
            throw new ArgumentException("InitialDelayMs cannot be negative.", nameof(InitialDelayMs));

        if (MaxDelayMs < InitialDelayMs)
            throw new ArgumentException("MaxDelayMs must be greater than or equal to InitialDelayMs.", nameof(MaxDelayMs));
    }
}
