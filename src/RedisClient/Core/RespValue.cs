namespace RedisClient.Core;

internal class RespValue
{
    public readonly RespType Type;
    public readonly byte[]? Value;
    public readonly RespValue[]? Values;

    public bool IsEmptyString() => Type == RespType.BulkString && Value is { Length: 0 };
    public bool IsNull() => Type == RespType.BulkString && Value is null;

    public RespValue(RespType type, byte[] value)
    {
        Type = type;
        Value = value;
    }

    public RespValue(RespValue[] values)
    {
        Type = RespType.Array;
        Values = values;
    }

    public override string ToString()
    {
        if (Value != null)
            return $"{Type}:{Value.AsEscapedString()}";
        if (Values != null)
            return string.Join<RespValue>(" | ", Values);
        return "";
    }
}