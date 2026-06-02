using System.Text.Json;
using Microsoft.Extensions.Options;
using SensorMon.Contracts;
using StackExchange.Redis;

namespace SensorMon.Alerter;

// Consumes high-temp alerts from the Valkey stream via a consumer group and pushes
// notifications to ntfy. The consumer group gives at-least-once delivery; this worker
// owns the "should I actually notify?" decision (debounce) AND duplicate suppression
// (idempotency) — both via one per-sensor cooldown key.
public sealed class AlerterWorker : BackgroundService
{
    private readonly ILogger<AlerterWorker> _log;
    private readonly IConnectionMultiplexer _mux;
    private readonly NtfyClient _ntfy;
    private readonly AlerterOptions _opts;

    // Identifies this consumer within the group. Using the pod/host name means that if
    // we ever scale to >1 replica each gets its own share of the stream automatically.
    private readonly string _consumerName = Environment.MachineName;

    // How often to poll when the stream is idle. StackExchange.Redis doesn't expose
    // XREADGROUP's BLOCK option, so we poll; at a handful of sensors this is negligible.
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(2);

    public AlerterWorker(
        ILogger<AlerterWorker> log,
        IConnectionMultiplexer mux,
        NtfyClient ntfy,
        IOptions<AlerterOptions> opts)
    {
        _log = log;
        _mux = mux;
        _ntfy = ntfy;
        _opts = opts.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var db = _mux.GetDatabase();
        await EnsureGroupAsync(db);

        _log.LogInformation(
            "Alerter '{Consumer}' reading group '{Group}' on stream '{Stream}'",
            _consumerName, AlertStream.Group, AlertStream.Key);

        // Phase 1: drain our own Pending Entries List (messages delivered to this
        // consumer before a crash but never acked). Reading from id "0" returns the
        // PEL; we process until it's empty, then switch to live messages.
        var position = "0";

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var entries = await db.StreamReadGroupAsync(
                    AlertStream.Key, AlertStream.Group, _consumerName,
                    position: position, count: 10);

                // Backlog drained → switch from PEL replay to new messages (">").
                if (position != ">" && entries.Length == 0)
                {
                    position = ">";
                    continue;
                }

                if (entries.Length == 0)
                {
                    await Task.Delay(PollInterval, stoppingToken);
                    continue;
                }

                foreach (var entry in entries)
                    await HandleEntryAsync(db, entry, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // normal shutdown
            }
            catch (Exception ex)
            {
                // Never let a transient Valkey error kill the consumer; back off and retry.
                _log.LogWarning(ex, "Stream read failed; retrying after backoff");
                await Task.Delay(PollInterval, stoppingToken);
            }
        }
    }

    private async Task HandleEntryAsync(IDatabase db, StreamEntry entry, CancellationToken ct)
    {
        var json = entry[AlertStream.PayloadField];
        Alert? alert = json.IsNullOrEmpty ? null : JsonSerializer.Deserialize<Alert>(json!);

        if (alert is null)
        {
            // Unparseable ("poison") message: ack it so it doesn't replay forever.
            _log.LogWarning("Dropping unparseable stream entry {Id}", entry.Id);
            await db.StreamAcknowledgeAsync(AlertStream.Key, AlertStream.Group, entry.Id);
            return;
        }

        // The one key that does both jobs. SET NX EX:
        //  - acquired  → first breach in the cooldown window → notify.
        //  - not acquired → still-hot repeat OR a redelivery → suppress (just ack).
        var cooldownKey = $"notified:{alert.Component}:{alert.SensorName}";
        var acquired = await db.StringSetAsync(
            cooldownKey, "1",
            expiry: TimeSpan.FromSeconds(_opts.CooldownSeconds),
            when: When.NotExists);

        if (!acquired)
        {
            _log.LogDebug("Suppressed (in cooldown): {Sensor}", alert.SensorName);
            await db.StreamAcknowledgeAsync(AlertStream.Key, AlertStream.Group, entry.Id);
            return;
        }

        try
        {
            await _ntfy.SendAsync(alert, ct);
            await db.StreamAcknowledgeAsync(AlertStream.Key, AlertStream.Group, entry.Id);
        }
        catch (Exception ex)
        {
            // Send failed: release the cooldown so the retry isn't suppressed, and do
            // NOT ack. The producer keeps publishing while the sensor stays hot, so a
            // fresh message will re-trigger us shortly — at-least-once self-heals.
            _log.LogWarning(ex, "ntfy send failed for {Sensor}; releasing cooldown", alert.SensorName);
            await db.KeyDeleteAsync(cooldownKey);
        }
    }

    // Create the consumer group if it doesn't exist yet (idempotent). MKSTREAM-equivalent
    // createStream:true means we don't depend on the worker having published first.
    private async Task EnsureGroupAsync(IDatabase db)
    {
        try
        {
            await db.StreamCreateConsumerGroupAsync(
                AlertStream.Key, AlertStream.Group,
                position: StreamPosition.NewMessages, createStream: true);
        }
        catch (RedisServerException ex) when (ex.Message.Contains("BUSYGROUP"))
        {
            // Group already exists — fine, this is the normal restart path.
        }
    }
}
