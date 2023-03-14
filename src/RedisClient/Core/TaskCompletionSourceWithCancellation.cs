namespace RedisClient.Core;

internal class TaskCompletionSourceWithCancellation<T> : TaskCompletionSource<T>
{
    public TaskCompletionSourceWithCancellation() : base(TaskCreationOptions.RunContinuationsAsynchronously)
    {
    }

    public async ValueTask<T> WaitWithCancellation(CancellationToken ct)
    {
        await using (ct.UnsafeRegister(static (s, cancellationToken) => ((TaskCompletionSourceWithCancellation<T>)s!).TrySetCanceled(cancellationToken), this))
            return await Task.ConfigureAwait(false);
    }
}