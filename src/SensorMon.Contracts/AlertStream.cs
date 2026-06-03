namespace SensorMon.Contracts;

// Names for stream key and group. no disagreements!
public static class AlertStream
{
    public const string Key = "alerts";

    public const string Group = "alerter";

    public const string PayloadField = "data";
}
