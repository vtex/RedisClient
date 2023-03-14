using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace RedisClient.Core;

internal static class SetRequestWriter
{
    private static ReadOnlySpan<byte> Header => Encoding.UTF8.GetBytes("*5\r\n$3\r\nSET\r\n");
    private static ReadOnlySpan<byte> ExOption => Encoding.UTF8.GetBytes("$2\r\nEX\r\n");

    public static ValueTask<FlushResult> Write(PipeWriter w, ReadOnlySpan<byte> key, ReadOnlySpan<byte> value, long ttl)
    {
        w.Write(Header);
        w.WriteBulkString(key);
        w.WriteBulkString(value);
        w.Write(ExOption);
        w.WriteBulkString(ttl);
        return w.FlushAsync();
    }
}