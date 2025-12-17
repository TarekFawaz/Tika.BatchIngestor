using Microsoft.Extensions.Logging;
using Tika.BatchIngestor.Abstractions;

namespace Tika.BatchIngestor.Internal;

internal class RetryExecutor
{
    private readonly RetryPolicy? _policy;
    private readonly ILogger? _logger;
    private static readonly Random _random = new();

    public RetryExecutor(RetryPolicy? policy, ILogger? logger)
    {
        _policy = policy;
        _logger = logger;
    }

    public async Task ExecuteAsync(
        Func<Task> operation,
        CancellationToken cancellationToken)
    {
        if (_policy == null || _policy.MaxRetries == 0)
        {
            await operation();
            return;
        }

        var attempt = 0;
        while (true)
        {
            try
            {
                await operation();
                return;
            }
            catch (Exception ex) when (attempt < _policy.MaxRetries && IsTransient(ex))
            {
                attempt++;
                var delay = CalculateDelay(attempt);
                
                _logger?.LogWarning(
                    ex,
                    "Transient failure on attempt {Attempt}/{MaxAttempts}. Retrying in {Delay}ms...",
                    attempt,
                    _policy.MaxRetries,
                    delay);

                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    private int CalculateDelay(int attempt)
    {
        if (_policy == null)
            return 0;

        var delay = _policy.InitialDelayMs;

        if (_policy.UseExponentialBackoff)
        {
            delay = (int)(_policy.InitialDelayMs * Math.Pow(2, attempt - 1));
        }
        else
        {
            delay = _policy.InitialDelayMs * attempt;
        }

        delay = Math.Min(delay, _policy.MaxDelayMs);

        if (_policy.UseJitter)
        {
            var jitter = _random.Next(0, delay / 4);
            delay += jitter;
        }

        return delay;
    }

    private bool IsTransient(Exception ex)
    {
        var message = ex.Message.ToLowerInvariant();
        
        if (message.Contains("timeout") ||
            message.Contains("deadlock") ||
            message.Contains("connection") ||
            message.Contains("network"))
        {
            return true;
        }

        if (ex is TimeoutException ||
            ex is System.IO.IOException ||
            ex is System.Net.Sockets.SocketException)
        {
            return true;
        }

        return false;
    }
}
