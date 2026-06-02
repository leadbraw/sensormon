namespace SensorMon.Alerter;

// Bound from the "Alerter" config section (appsettings.json / env vars).
public sealed class AlerterOptions
{
    // Valkey connection string, e.g. "valkey:6379". Same instance the worker publishes to.
    public string ValkeyConnection { get; set; } = "localhost:6379";

    // ntfy base URL. Public ntfy.sh by default; could point at a self-hosted instance.
    public string NtfyBaseUrl { get; set; } = "https://ntfy.sh";

    // ntfy topic to publish to. Treat as semi-secret: anyone who knows it can read
    // your alerts, so it's injected from the k8s Secret, not the ConfigMap.
    public string NtfyTopic { get; set; } = "";

    // Per-sensor cooldown (seconds). After alerting on a sensor we stay quiet for this
    // long. This single window is BOTH the debounce (no spam while a sensor stays hot)
    // AND the idempotency guard (a redelivered message lands in the same window and is
    // suppressed). 300s = re-alert at most every 5 minutes while still hot.
    public int CooldownSeconds { get; set; } = 300;
}
