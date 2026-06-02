namespace SensorMon.Contracts;

// The message that crosses the broker: one breaching temperature reading.
// A record gives value-equality and a compact, immutable shape. It's serialized
// to JSON and carried in a single Valkey stream field (see AlertStream.PayloadField).
//
// There is deliberately no "AlertId" field: the consumer derives its dedup/debounce
// key from Component + SensorName, so the same hot sensor always maps to the same
// key regardless of which reading triggered it.
public sealed record Alert(
    string Component,
    string SensorName,
    string SensorType,
    double Value,
    string Unit,
    double Threshold,
    DateTime Timestamp);
