using System.Net.Sockets;
using System.Text;
using Microsoft.Extensions.Options;

namespace Rezerv.BookingEngine.Infrastructure.Redis;

/// <summary>
/// Small RESP2 client for the limited Redis operations this assessment needs.
/// A production service would use StackExchange.Redis for pooling, retries and telemetry.
/// </summary>
public sealed class RespRedisClient(IOptions<RedisOptions> options) : IRedisClient
{
    private readonly RedisOptions _options = options.Value;

    public async Task<string?> GetStringAsync(string key, CancellationToken cancellationToken) =>
        await ExecuteAsync(["GET", key], cancellationToken);

    public async Task SetStringAsync(
        string key, string value, TimeSpan expiry, CancellationToken cancellationToken) =>
        _ = await ExecuteAsync(
            ["SET", key, value, "PX", ((long)expiry.TotalMilliseconds).ToString()], cancellationToken);

    public async Task<long> IncrementAsync(string key, CancellationToken cancellationToken)
    {
        var value = await ExecuteAsync(["INCR", key], cancellationToken);
        return long.Parse(value!, System.Globalization.CultureInfo.InvariantCulture);
    }

    public async Task<bool> TryAcquireLockAsync(
        string key, string token, TimeSpan expiry, CancellationToken cancellationToken)
    {
        var result = await ExecuteAsync(
            ["SET", key, token, "NX", "PX", ((long)expiry.TotalMilliseconds).ToString()],
            cancellationToken);
        return result == "OK";
    }

    public async Task ReleaseLockAsync(string key, string token, CancellationToken cancellationToken)
    {
        const string script = "if redis.call('get', KEYS[1]) == ARGV[1] then " +
                              "return redis.call('del', KEYS[1]) else return 0 end";
        _ = await ExecuteAsync(["EVAL", script, "1", key, token], cancellationToken);
    }

    public async Task<bool> PingAsync(CancellationToken cancellationToken) =>
        await ExecuteAsync(["PING"], cancellationToken) == "PONG";

    private async Task<string?> ExecuteAsync(string[] command, CancellationToken cancellationToken)
    {
        using var client = new TcpClient();
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_options.ConnectTimeoutMilliseconds);
        await client.ConnectAsync(_options.Host, _options.Port, timeout.Token);
        await using var stream = client.GetStream();

        if (!string.IsNullOrEmpty(_options.Password))
        {
            await WriteCommandAsync(stream, ["AUTH", _options.Password], timeout.Token);
            _ = await ReadResponseAsync(stream, timeout.Token);
        }

        if (_options.Database != 0)
        {
            await WriteCommandAsync(stream, ["SELECT", _options.Database.ToString()], timeout.Token);
            _ = await ReadResponseAsync(stream, timeout.Token);
        }

        await WriteCommandAsync(stream, command, timeout.Token);
        return await ReadResponseAsync(stream, timeout.Token);
    }

    private static async Task WriteCommandAsync(
        Stream stream, IReadOnlyList<string> command, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder($"*{command.Count}\r\n");
        foreach (var argument in command)
        {
            var bytes = Encoding.UTF8.GetByteCount(argument);
            builder.Append('$').Append(bytes).Append("\r\n").Append(argument).Append("\r\n");
        }

        var payload = Encoding.UTF8.GetBytes(builder.ToString());
        await stream.WriteAsync(payload, cancellationToken);
    }

    private static async Task<string?> ReadResponseAsync(Stream stream, CancellationToken cancellationToken)
    {
        var prefix = (char)await ReadByteAsync(stream, cancellationToken);
        var line = await ReadLineAsync(stream, cancellationToken);
        return prefix switch
        {
            '+' or ':' => line,
            '$' when line == "-1" => null,
            '$' => await ReadBulkStringAsync(stream, int.Parse(line), cancellationToken),
            '-' => throw new InvalidOperationException($"Redis error: {line}"),
            _ => throw new InvalidOperationException($"Unsupported Redis response prefix '{prefix}'.")
        };
    }

    private static async Task<string> ReadBulkStringAsync(
        Stream stream, int length, CancellationToken cancellationToken)
    {
        var buffer = new byte[length];
        var read = 0;
        while (read < length)
        {
            var count = await stream.ReadAsync(buffer.AsMemory(read, length - read), cancellationToken);
            if (count == 0)
            {
                throw new EndOfStreamException("Redis closed the connection.");
            }

            read += count;
        }

        _ = await ReadByteAsync(stream, cancellationToken);
        _ = await ReadByteAsync(stream, cancellationToken);
        return Encoding.UTF8.GetString(buffer);
    }

    private static async Task<string> ReadLineAsync(Stream stream, CancellationToken cancellationToken)
    {
        var bytes = new List<byte>();
        while (true)
        {
            var current = await ReadByteAsync(stream, cancellationToken);
            if (current == '\r')
            {
                _ = await ReadByteAsync(stream, cancellationToken);
                return Encoding.UTF8.GetString(bytes.ToArray());
            }

            bytes.Add((byte)current);
        }
    }

    private static async Task<int> ReadByteAsync(Stream stream, CancellationToken cancellationToken)
    {
        var buffer = new byte[1];
        var count = await stream.ReadAsync(buffer, cancellationToken);
        return count == 0 ? throw new EndOfStreamException("Redis closed the connection.") : buffer[0];
    }
}

