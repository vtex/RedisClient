using System.Diagnostics;
using System.IO.Pipelines;
using System.Net.Sockets;

namespace RedisClient.Core;

internal class Connection : IDisposable
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();
    private readonly TcpClient _tcp;
    private readonly AtomicCounter _counter;
    private readonly TimeSpan _lifespan;
    private bool _disposed;

    public DuplexPipe Transport { get; }
    public TimeSpan Elapsed => _sw.Elapsed;
    public bool IsValid => _sw.Elapsed < _lifespan;

    public Connection(TcpClient tcp, TimeSpan lifespan, AtomicCounter counter)
    {
        var jit = Random.Shared.Next((int)(lifespan.TotalSeconds * 0.3));
        _lifespan = lifespan.Add(TimeSpan.FromSeconds(jit));
        _tcp = tcp;
        _counter = counter;
        var stream = tcp.GetStream();
        var reader = PipeReader.Create(stream, new StreamPipeReaderOptions(leaveOpen: true));
        var writer = PipeWriter.Create(stream, new StreamPipeWriterOptions(leaveOpen: true));
        Transport = new DuplexPipe(reader, writer);
        counter.Increment();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _counter.Decrement();
        try
        {
            _tcp.Dispose();
        }
        catch
        {
            //noop
        }
    }
}