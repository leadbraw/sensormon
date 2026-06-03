namespace SensorMon.Contracts;

// no ID field, not needed
public sealed record Alert(
    string Component,
    string SensorName,
    string SensorType,
    double Value,
    string Unit,
    double Threshold,
    DateTime Timestamp);
