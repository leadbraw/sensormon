namespace SensorMon.Alerter;

// Bound from appsettings.json
public sealed class AlerterOptions
{
    public string ValkeyConnection { get; set; } = "localhost:6379";

    public string NtfyBaseUrl { get; set; } = "https://ntfy.sh";

    // defined in k8s Secret
    public string NtfyTopic { get; set; } = "";

    // 5 min (per sensor)
    public int CooldownSeconds { get; set; } = 300;

    // fixed name so new pods can read the PEL of old ones (e.g. on crash/restart)
    public string ConsumerName { get; set; } = "alerter-0";
}
