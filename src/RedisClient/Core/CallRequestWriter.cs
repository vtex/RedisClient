using System.IO.Pipelines;
using System.Text;

namespace RedisClient.Core;

internal static class CallRequestWriter
{
    public static ValueTask<FlushResult> Write(PipeWriter w, params string[] @params)
    {
        w.WriteChar('*');
        w.WriteLong(@params.Length);
        foreach (var value in @params)
        {
            var encoding = Encoding.UTF8;
            var max = encoding.GetMaxByteCount(value.Length);
            var buffer = w.GetSpan(max);
            var length = encoding.GetBytes(value, buffer);
            w.Advance(length);
        }
        return w.FlushAsync();
    }
}