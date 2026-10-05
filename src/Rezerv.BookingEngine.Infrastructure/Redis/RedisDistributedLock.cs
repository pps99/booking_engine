namespace Rezerv.BookingEngine.Infrastructure.Redis;

public interface IDistributedLockProvider
{
    Task<IAsyncDisposable> AcquireAsync(string resource, CancellationToken cancellationToken);
}

public sealed class RedisDistributedLockProvider(IRedisClient redisClient) : IDistributedLockProvider
{
    private static readonly TimeSpan Lease = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromMilliseconds(50);
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(5);

    public async Task<IAsyncDisposable> AcquireAsync(
        string resource, CancellationToken cancellationToken)
    {
        var key = $"rezerv:lock:{resource}";
        var token = Guid.NewGuid().ToString("N");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(WaitTimeout);

        try
        {
            while (!await redisClient.TryAcquireLockAsync(key, token, Lease, timeout.Token))
            {
                await Task.Delay(RetryDelay, timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new Rezerv.BookingEngine.Application.BookingEngineException(
                "schedule_busy",
                "The schedule is busy. Please retry the request.",
                409);
        }

        return new RedisLockHandle(redisClient, key, token);
    }

    private sealed class RedisLockHandle(IRedisClient client, string key, string token) : IAsyncDisposable
    {
        public async ValueTask DisposeAsync()
        {
            try
            {
                await client.ReleaseLockAsync(key, token, CancellationToken.None);
            }
            catch
            {
                // The short lease is the final safety net if Redis is unavailable during release.
            }
        }
    }
}
