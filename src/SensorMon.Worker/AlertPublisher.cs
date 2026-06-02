using System.Text.Json;
using SensorMon.Contracts;
using StackExchange.Redis;

namespace SensorMon.Worker;

// Publishes a high-temp Alert onto the Valkey stream. Kept behind an interface so
// PollingWorker depends on the intent ("publish an alert"), not on Redis specifics.
public interface IAlertPublisher
{
    Task PublishAsync(Alert alert, CancellationToken ct);
}

public sealed class RedisAlertPublisher : IAlertPublisher
{
    private readonly IConnectionMultiplexer _mux;

    public RedisAlertPublisher(IConnectionMultiplexer mux) => _mux = mux;

    public async Task PublishAsync(Alert alert, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(alert);
        var db = _mux.GetDatabase();

        // XADD alerts MAXLEN ~ 10000 * data {json}
        // useApproximateMaxLength (the "~") lets Valkey trim in efficient chunks so
        // the stream self-caps instead of growing forever — we never read old history.
        await db.StreamAddAsync(
            AlertStream.Key,
            AlertStream.PayloadField,
            json,
            maxLength: 10_000,
            useApproximateMaxLength: true);
    }
}
