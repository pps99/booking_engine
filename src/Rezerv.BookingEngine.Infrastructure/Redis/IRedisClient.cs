namespace Rezerv.BookingEngine.Infrastructure.Redis;

public interface IRedisClient
{
    Task<string?> GetStringAsync(string key, CancellationToken cancellationToken);
    Task SetStringAsync(string key, string value, TimeSpan expiry, CancellationToken cancellationToken);
    Task<long> IncrementAsync(string key, CancellationToken cancellationToken);
    Task<bool> TryAcquireLockAsync(string key, string token, TimeSpan expiry, CancellationToken cancellationToken);
    Task ReleaseLockAsync(string key, string token, CancellationToken cancellationToken);
    Task<bool> PingAsync(CancellationToken cancellationToken);
}

