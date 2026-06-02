using Microsoft.Extensions.Options;
using SensorMon.Contracts;

namespace SensorMon.Alerter;

// Sends a push notification to ntfy.sh. A POST to {BaseUrl}/{Topic} with the message
// as the body delivers to every device subscribed to that topic. Throws on failure so
// the caller can decide whether to ack the stream message or leave it for retry.
public sealed class NtfyClient
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly AlerterOptions _opts;
    private readonly ILogger<NtfyClient> _log;

    public NtfyClient(
        IHttpClientFactory httpFactory,
        IOptions<AlerterOptions> opts,
        ILogger<NtfyClient> log)
    {
        _httpFactory = httpFactory;
        _opts = opts.Value;
        _log = log;
    }

    public async Task SendAsync(Alert alert, CancellationToken ct)
    {
        var client = _httpFactory.CreateClient("ntfy");

        var url = $"{_opts.NtfyBaseUrl.TrimEnd('/')}/{_opts.NtfyTopic}";
        var body =
            $"{alert.Component}\n{alert.SensorName} is {alert.Value:0.#}{alert.Unit} " +
            $"(threshold {alert.Threshold:0.#}{alert.Unit}) at {alert.Timestamp:HH:mm:ss}";

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(body),
        };
        // ntfy reads these headers to set the notification's title, urgency, and icons.
        // headers must be ascii, ntfy will autofill emojis for the warning & fire tags
        req.Headers.TryAddWithoutValidation("Title", $"High temp: {alert.Component}");
        req.Headers.TryAddWithoutValidation("Priority", "high");
        req.Headers.TryAddWithoutValidation("Tags", "warning,fire");

        using var resp = await client.SendAsync(req, ct);
        resp.EnsureSuccessStatusCode();

        _log.LogInformation("Sent ntfy notification for {Sensor}", alert.SensorName);
    }
}
