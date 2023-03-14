using System.Buffers;
using System.IO.Pipelines;
using System.Text;

namespace RedisClient.Core;

internal static class GetRequestWriter
{
    private static ReadOnlySpan<byte> Header => Encoding.UTF8.GetBytes("*2\r\n$3\r\nGET\r\n");

    public static ValueTask<FlushResult> Write(PipeWriter w, ReadOnlySpan<byte> key)
    {
        w.Write(Header);
        w.WriteBulkString(key);
        return w.FlushAsync();
    }
}