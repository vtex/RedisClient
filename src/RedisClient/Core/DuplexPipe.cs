using System.IO.Pipelines;

namespace RedisClient.Core;

internal class DuplexPipe : IDuplexPipe
{
    public DuplexPipe(PipeReader input, PipeWriter output)
    {
        Input = input;
        Output = output;
    }
    public PipeReader Input { get; }
    public PipeWriter Output { get; }
}