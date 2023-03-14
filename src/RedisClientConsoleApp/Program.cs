//var ip = IPAddress.Parse("127.0.0.1");

//using var conn = await Connection.Connect(ip, 6379);

//var writes = conn.Transport.Input.CopyToAsync(Console.OpenStandardOutput());

//await SetRequestWriter.Write(conn.Transport.Output, Encoding.UTF8.GetBytes("some-key"), Encoding.UTF8.GetBytes("some-value"), 10);
//await Task.Delay(1000);
//await GetRequestWriter.Write(conn.Transport.Output, Encoding.UTF8.GetBytes("some-key"));

//await writes;

using var client = new RedisClient.RedisClient(host: "localhost", port: 6379, lifespan: TimeSpan.FromMinutes(3));

for (var i = 0; i < 10_000; i++)
{
    var key = $"some-key-{i}";
    var value = $"some-value-{i}";

    await client.Set(key: key, value: value, ttl: 10);

    var result = await client.Get(key);

    if (result != value)
        throw new Exception($"Iteration {i} failed: {value}, {result}");
}

Console.WriteLine("done");

return;


Console.WriteLine(await client.Get("foo") ?? "[null]");

await client.Set(key: "foo", value: "bar", ttl: 30);

Console.WriteLine(await client.Get("foo") ?? "[null]");