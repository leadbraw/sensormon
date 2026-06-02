using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SensorMon.Contracts;

namespace SensorMon.Worker;

// BackgroundService is the .NET base class for long-running background tasks.
// The host starts ExecuteAsync on startup and signals the CancellationToken on shutdown.
// This is THE idiomatic way to do "run a loop forever" work — name-drop it in the interview.
public sealed class PollingWorker : BackgroundService
{
    private readonly ILogger<PollingWorker> _log;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IAlertPublisher _publisher;
    private readonly SensorOptions _opts;

    public PollingWorker(
        ILogger<PollingWorker> log,
        IHttpClientFactory httpFactory,
        IServiceScopeFactory scopeFactory,
        IAlertPublisher publisher,
        IOptions<SensorOptions> opts)
    {
        _log = log;
        _httpFactory = httpFactory;
        _scopeFactory = scopeFactory;
        _publisher = publisher;
        _opts = opts.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Polling {Endpoint} every {Seconds}s", _opts.Endpoint, _opts.PollSeconds);

        // PeriodicTimer is the modern, allocation-friendly way to do a fixed-interval loop.
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(_opts.PollSeconds));

        // Run one poll immediately, then on each tick.
        do
        {
            try
            {
                await PollOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break; // normal shutdown
            }
            catch (Exception ex)
            {
                // CRITICAL: never let a failed poll crash the service. The desktop
                // is often off — we log and keep going. This is the resilience story.
                _log.LogWarning(ex, "Poll failed; will retry next interval");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task PollOnceAsync(CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("lhm");

        // Short timeout: if the desktop is off, fail fast and move on.
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));

        var json = await client.GetStringAsync(_opts.Endpoint, cts.Token);
        var root = JsonSerializer.Deserialize<LhmNode>(json);
        if (root is null)
        {
            _log.LogWarning("Got null/empty JSON from endpoint");
            return;
        }

        var now = DateTime.Now;
        var readings = LhmParser.Flatten(root, now, _opts.Components).ToList();
        if (readings.Count == 0)
        {
            _log.LogWarning("No parseable readings in response");
            return;
        }

        // A DbContext is a short-lived unit of work — create a scope per poll
        // rather than holding one open for the life of the singleton worker.
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SensorDbContext>();

        db.Readings.AddRange(readings);
        await db.SaveChangesAsync(ct);

        _log.LogInformation("Stored {Count} readings at {Time}", readings.Count, now);

        // --- event-driven alert hook ----------------------------------------
        // Publish a HighTempAlert for each breaching temperature reading. This runs
        // AFTER SaveChanges, on purpose: the publish is OUTSIDE the DB transaction
        // (the dual-write is decoupled). Delivery is at-least-once, so the consumer
        // (Alerter) is responsible for debounce + idempotency.
        //
        // We don't debounce here — the worker stays dumb and publishes every breach;
        // the Alerter's per-sensor cooldown collapses repeats AND redeliveries into
        // one notification. Each publish is isolated so a Valkey hiccup can't crash
        // the poll loop (same resilience contract as the rest of this service).
        foreach (var r in readings)
        {
            if (r.SensorType != "Temperature" || r.Value < _opts.HighTempThreshold)
                continue;

            try
            {
                var alert = new Alert(
                    r.Component, r.SensorName, r.SensorType,
                    r.Value, r.Unit, _opts.HighTempThreshold, r.Timestamp);
                await _publisher.PublishAsync(alert, ct);
                _log.LogInformation(
                    "Published high-temp alert: {Sensor} = {Value}{Unit} (>= {Threshold})",
                    r.SensorName, r.Value, r.Unit, _opts.HighTempThreshold);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Failed to publish alert for {Sensor}", r.SensorName);
            }
        }
        // --------------------------------------------------------------------
    }
}
