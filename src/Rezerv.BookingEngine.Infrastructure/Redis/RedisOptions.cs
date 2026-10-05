namespace Rezerv.BookingEngine.Infrastructure.Redis;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";
    public string Host { get; init; } = "localhost";
    public int Port { get; init; } = 6379;
    public string? Password { get; init; }
    public int Database { get; init; }
    public int ConnectTimeoutMilliseconds { get; init; } = 2_000;
}

