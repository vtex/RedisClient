namespace RedisClient.Core;

internal class RespArrayState
{
    public long Remaining;
    public readonly List<RespValue> Values = new();
}