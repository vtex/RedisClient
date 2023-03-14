using System.Text;
using RedisClient.Core;

namespace RedisClient;

public class RedisClient : IDisposable
{
    private readonly ConnectionPool _pool;

    public RedisClient(string host, int port, TimeSpan lifespan) => _pool = new ConnectionPool(host, port, lifespan);

    internal async ValueTask<string?> Get(string key, CancellationToken ct = default)
    {
        var result = await Get(Encoding.UTF8.GetBytes(key), ct);
        return result is null or { Length: 0 } ? null : Encoding.UTF8.GetString(result);
    }

    public async ValueTask<byte[]?> Get(ReadOnlyMemory<byte> key, CancellationToken ct)
    {
        using var lease = await _pool.Connect(ct);
        var conn = lease.Conn;
        try
        {
            await GetRequestWriter.Write(conn.Transport.Output, key.Span);
            var reader = new ResponseReader(conn.Transport.Input, ct);
            var close = await reader.Read();
            var content = reader.GetContent();
            lease.Failing = close;
            return content.Value;
        }
        catch
        {
            lease.Failing = true;
            throw;
        }
    }

    internal ValueTask Set(string key, string value, long ttl, CancellationToken ct = default)
    {
        return Set(Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value), ttl, ct);
    }

    public async ValueTask Set(ReadOnlyMemory<byte> key, ReadOnlyMemory<byte> value, long ttl, CancellationToken ct)
    {
        using var lease = await _pool.Connect(ct);
        var conn = lease.Conn;
        try
        {
            await SetRequestWriter.Write(conn.Transport.Output, key.Span, value.Span, ttl);
            var reader = new ResponseReader(conn.Transport.Input, ct);
            var close = await reader.Read();
            var content = reader.GetContent();
            lease.Failing = close;

            if (content.Type == RespType.Error)
                throw new Exception(Encoding.UTF8.GetString(content.Value!));

            if (!content.Value.AsSpan().SequenceEqual(OK))
                throw new Exception("Failed to set key.");
        }
        catch
        {
            lease.Failing = true;
            throw;
        }
    }

    private static byte[] OK => Encoding.UTF8.GetBytes("OK");

    public void Dispose()
    {
        _pool.Dispose();
    }
}