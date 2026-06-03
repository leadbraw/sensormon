namespace SensorMon.Worker;

public sealed class SensorOptions
{
    // Defined in appsettings.json
    public string Endpoint { get; set; } = "";

    public int PollSeconds { get; set; } = 5;

    // threshold (celsius) for alert to fire
    public double HighTempThreshold { get; set; } = 85.0;

    // The readings we care about
    public List<ComponentFilter> Components { get; set; } = [];
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
