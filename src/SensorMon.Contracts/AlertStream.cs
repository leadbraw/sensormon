namespace SensorMon.Contracts;

// Names for the Valkey Stream the producer writes to and the consumer group the
// Alerter reads with. Kept here so producer and consumer can never disagree on them.
public static class AlertStream
{
    // The stream key. Producer does XADD here; consumer reads via the group below.
    public const string Key = "alerts";

    // The consumer group name. A group gives at-least-once delivery: each message
    // stays in the group's Pending Entries List until a consumer XACKs it, so a
    // restarted Alerter resumes instead of losing messages (unlike Pub/Sub).
    public const string Group = "alerter";

    // The single stream field the JSON-serialized Alert is stored under.
    public const string PayloadField = "data";
}
