namespace RedisClient.Core;

public enum RespType
{
    Undefined,
    SimpleString,
    Error,
    Integer,
    Array,
    BulkString
}