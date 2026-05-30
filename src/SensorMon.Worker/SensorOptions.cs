namespace SensorMon.Worker;

// Bound from the "Sensor" section of configuration (appsettings.json / env vars).
// Using an options class instead of reading config strings everywhere is the
// idiomatic .NET pattern and is worth mentioning in an interview.
public sealed class SensorOptions
{
    // The LibreHardwareMonitor JSON endpoint on your desktop, e.g. http://192.168.1.50:8085/data.json
    public string Endpoint { get; set; } = "";

    // How often to poll, in seconds.
    public int PollSeconds { get; set; } = 5;

    // Temperature (°C) above which a reading is considered "high".
    // Used later for the optional event-driven alert piece.
    public double HighTempThreshold { get; set; } = 85.0;

    // Which hardware components to capture, and which sensors within each.
    // Anything not listed here is skipped — this is what trims a ~230-reading
    // poll down to the handful we actually care about.
    public List<ComponentFilter> Components { get; set; } = new();
}

// One hardware component (e.g. the CPU or GPU) we want to record, plus a
// per-category allow-list of sensor names within it.
public sealed class ComponentFilter
{
    // The LHM hardware node's Text, matched exactly, e.g. "AMD Ryzen 7 5700X3D".
    public string Match { get; set; } = "";

    // Category name (e.g. "Temperatures", "Load", "Data") -> sensor names to keep.
    // A list containing "*" means "every sensor in this category".
    public Dictionary<string, List<string>> Categories { get; set; } = new();
}
