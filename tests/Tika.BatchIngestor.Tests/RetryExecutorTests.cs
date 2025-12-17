using Tika.BatchIngestor.Abstractions;
using Tika.BatchIngestor.Internal;
using Xunit;

namespace Tika.BatchIngestor.Tests;

public class RetryExecutorTests
{
    [Fact]
    public async Task ExecuteAsync_SucceedsOnFirstAttempt()
    {
        var policy = new RetryPolicy { MaxRetries = 3 };
        var executor = new RetryExecutor(policy, null);
        var callCount = 0;

        await executor.ExecuteAsync(async () =>
        {
            callCount++;
            await Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(1, callCount);
    }

    [Fact]
    public async Task ExecuteAsync_RetriesOnTransientFailure()
    {
        var policy = new RetryPolicy 
        { 
            MaxRetries = 3,
            InitialDelayMs = 10,
            UseExponentialBackoff = false,
            UseJitter = false
        };
        var executor = new RetryExecutor(policy, null);
        var callCount = 0;

        await executor.ExecuteAsync(async () =>
        {
            callCount++;
            if (callCount < 3)
            {
                throw new TimeoutException("Simulated timeout");
            }
            await Task.CompletedTask;
        }, CancellationToken.None);

        Assert.Equal(3, callCount);
    }
}
