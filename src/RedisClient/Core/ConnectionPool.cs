using System.Collections.Concurrent;
using System.Net.Sockets;

namespace RedisClient.Core;

internal class ConnectionPool
{
    private readonly DnsMap _dns;
    private readonly int _port;
    private readonly TimeSpan _lifespan;
    private readonly AtomicCounter _counter = new();

    public class Lease : IDisposable
    {
        public ConnectionPool Pool { get; }
        public Connection Conn { get; }
        public bool Failing { get; set; }
        public Lease(ConnectionPool pool, Connection conn)
        {
            Pool = pool;
            Conn = conn;
        }
        public void Dispose()
        {
            if (Failing)
                Conn.Dispose();
            else
                Pool.Return(Conn);
        }
    }

    public int TotalConnections => _counter.Value;

    private readonly ConcurrentQueue<Connection> _pool = new();

    public ConnectionPool(string host, int port, TimeSpan lifespan)
    {
        _dns = DnsMap.Create(host);
        _port = port;
        _lifespan = lifespan;
    }

    public ValueTask<Lease> Connect(CancellationToken ct)
    {
        while (_pool.TryDequeue(out var conn))
        {
            if (conn.IsValid)
                return new ValueTask<Lease>(new Lease(this, conn));
            conn.Dispose();
        }
        return ConnectSlow(ct);
    }

    public void Return(Connection conn)
    {
        if (conn.IsValid)
            _pool.Enqueue(conn);
        else
            conn.Dispose();
    }

    private async ValueTask<Lease> ConnectSlow(CancellationToken ct)
    {
        TcpClient? tcp = null;
        try
        {
            tcp = new TcpClient();
            await tcp.ConnectAsync(_dns.GetRandomIp(), _port, ct);
            return new Lease(this, new Connection(tcp, _lifespan, _counter));
        }
        catch
        {
            tcp?.Dispose();
            throw;
        }
    }

    public void Dispose()
    {
        //this will miss connections in flight
        while (_pool.TryDequeue(out var conn))
            conn.Dispose();
    }
}