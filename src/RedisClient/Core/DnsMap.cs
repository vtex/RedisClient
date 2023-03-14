using System.Net;

namespace RedisClient.Core;

internal class DnsMap : IDisposable
{
    private readonly string _host;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _refreshLoop;
    private IPAddress[] _ips;

    private DnsMap(string host, IPAddress[] ips)
    {
        _host = host;
        _ips = ips;
        _refreshLoop = Refresh();
    }

    public static DnsMap Create(string host)
    {
        //in linux, resolving dns is a sync operation, maybe offset this to a new thread.
        var ips = Dns.GetHostAddresses(host);
        if (ips.Length == 0)
            throw new Exception($"Host '{host}' returned no ips.");

        return new DnsMap(host, ips);
    }

    private async Task Refresh()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(_cts.Token))
            {
                try
                {
                    var ips = await Dns.GetHostAddressesAsync(_host);
                    if (ips.Length > 0)
                        _ips = ips;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }
        catch (OperationCanceledException e) when (e.CancellationToken == _cts.Token)
        {
            //stopping
        }
    }

    public IPAddress GetRandomIp() => _ips.Length == 1 ? _ips[0] : _ips[Random.Shared.Next(_ips.Length)];

    public void Dispose()
    {
        try
        {
            _cts.Cancel();
            _refreshLoop.Wait(TimeSpan.FromSeconds(3));
        }
        catch
        {
            //disposing
        }
    }
}