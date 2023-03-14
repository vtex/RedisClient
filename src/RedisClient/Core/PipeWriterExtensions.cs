using System.Buffers;
using System.Buffers.Text;
using System.IO.Pipelines;
using System.Runtime.CompilerServices;

namespace RedisClient.Core;

internal static class PipeWriterExtensions
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteCRLF(this PipeWriter w)
    {
        var span = w.GetSpan(2);
        span[0] = (byte)'\r';
        span[1] = (byte)'\n';
        w.Advance(2);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteChar(this PipeWriter w, char value)
    {
        var span = w.GetSpan(1);
        span[0] = (byte)value;
        w.Advance(1);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteLong(this PipeWriter w, long value)
    {
        var buffer = w.GetSpan(20);
        Utf8Formatter.TryFormat(value, buffer, out var length);
        w.Advance(length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteBulkStringSize(this PipeWriter w, int size)
    {
        w.WriteChar('$');
        w.WriteLong(size);
        w.WriteCRLF();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void WriteBulkString(this PipeWriter w, ReadOnlySpan<byte> value)
    {
        w.WriteBulkStringSize(value.Length);
        w.Write(value);
        w.WriteCRLF();
    }

    public static void WriteBulkString(this PipeWriter w, long value)
    {
        Span<byte> buffer = stackalloc byte[20];
        Utf8Formatter.TryFormat(value, buffer, out var bytesWritten);
        w.WriteBulkStringSize(bytesWritten);
        w.Write(buffer.Slice(0, bytesWritten));
        w.WriteCRLF();
    }
}