namespace RedisClient.Core;

internal class AtomicCounter
{
    private int _value;
    public int Value => _value;
    public void Increment() => Interlocked.Increment(ref _value);
    public void Decrement() => Interlocked.Increment(ref _value);
}