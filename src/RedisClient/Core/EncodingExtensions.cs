using System.Text;

namespace RedisClient.Core;

internal static class EncodingExtensions
{
    public static byte[] AsBytes(this string value) =>
        Encoding.UTF8.GetBytes(value);

    public static string AsEscapedString(this byte[] value) =>
        Encoding.UTF8.GetString(value).Replace("\r", "\\r").Replace("\n", "\\n");

    public static string AsEscapedString(this ReadOnlySpan<byte> value) =>
        Encoding.UTF8.GetString(value).Replace("\r", "\\r").Replace("\n", "\\n");
}