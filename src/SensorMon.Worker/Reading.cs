namespace SensorMon.Worker;

// One row in the database = one sensor value at one point in time.
// This "long, narrow" shape (timestamp + name + value) is the typical
// way to store time-series-ish data in a relational DB.
public sealed class Reading
{
    public long Id { get; set; }

    // When the reading was taken, in the worker's LOCAL time (wall clock).
    // Stored as Postgres `timestamp without time zone` — see SensorDbContext.
    public DateTime Timestamp { get; set; }

    // The hardware component this reading came from, e.g. "AMD Ryzen 7 5700X3D".
    // Stored separately so you can filter/group by component without parsing SensorName.
    public string Component { get; set; } = "";

    // Fully-qualified name "Component / Category / Sensor",
    // e.g. "AMD Ryzen 7 5700X3D / Load / CPU Total".
    public string SensorName { get; set; } = "";

    // e.g. "Temperature", "Load", "Power"
    public string SensorType { get; set; } = "";

    // The numeric value.
    public double Value { get; set; }

    // The unit, e.g. "°C", "%", "W"
    public string Unit { get; set; } = "";
}
