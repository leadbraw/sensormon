using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace SensorMon.Worker;

// BackgroundService is the .NET base class for long-running background tasks.
// The host starts ExecuteAsync on startup and signals the CancellationToken on shutdown.
// This is THE idiomatic way to do "run a loop forever" work — name-drop it in the interview.
public sealed class PollingWorker : BackgroundService
{
    private readonly ILogger<PollingWorker> _log;
    private readonly IHttpClientFactory _httpFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly SensorOptions _opts;

    public PollingWorker(
        ILogger<PollingWorker> log,
        IHttpClientFactory httpFactory,
        IServiceScopeFactory scopeFactory,
        IOptions<SensorOptions> opts)
    {
        _log = log;
        _httpFactory = httpFactory;
        _scopeFactory = scopeFactory;
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

        // --- OPTIONAL event-driven hook -------------------------------------
        // When you add the alerting piece, this is where you'd check the
        // threshold and publish an event to Redis/NATS:
        //
        //   var hot = readings.Where(r =>
        //       r.SensorType == "Temperature" && r.Value >= _opts.HighTempThreshold);
        //   foreach (var r in hot)
        //       await _publisher.PublishHighTempAsync(r, ct);
        //
        // Keep the publish OUT of the same DB transaction; the consumer should
        // be idempotent because delivery is at-least-once.
        // --------------------------------------------------------------------
    }
}
