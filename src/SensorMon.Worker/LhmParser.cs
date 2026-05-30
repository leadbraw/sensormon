using System.Globalization;
using System.Text.Json.Serialization;

namespace SensorMon.Worker;

// LibreHardwareMonitor's /data.json is a recursive tree. Every node has the
// same shape: a Text label, an optional Value string, a Type, and Children.
// Hardware/category nodes have empty Value and a populated Children list;
// leaf nodes (actual sensors) have a Value like "45.3 °C" and no children.
public sealed class LhmNode
{
    [JsonPropertyName("Text")]
    public string Text { get; set; } = "";

    // NOTE: value + unit are jammed into one string, e.g. "45.3 °C", "12.0 %", "65.0 W".
    [JsonPropertyName("Value")]
    public string Value { get; set; } = "";

    // e.g. "Temperature", "Load", "Power", "Clock". Empty on non-leaf nodes.
    [JsonPropertyName("Type")]
    public string Type { get; set; } = "";

    [JsonPropertyName("Children")]
    public List<LhmNode> Children { get; set; } = new();
}

public static class LhmParser
{
    // The LHM tree is: Sensor -> Computer -> Hardware -> Category -> Sensor leaf.
    // Rather than flatten everything, we keep only the hardware components named in
    // the config, and within each only the categories/sensors listed for it.
    public static IEnumerable<Reading> Flatten(
        LhmNode root, DateTime timestamp, IReadOnlyList<ComponentFilter> components)
    {
        foreach (var componentNode in FindComponentNodes(root, components))
        {
            // FindComponentNodes only yields nodes whose Text matches a filter, so this is present.
            var filter = components.First(c => c.Match == componentNode.Text);

            // A component's direct children are its categories (Temperatures, Load, Data, ...).
            foreach (var category in componentNode.Children)
            {
                if (!filter.Categories.TryGetValue(category.Text, out var allowedNames))
                    continue; // category not requested for this component

                bool keepAll = allowedNames.Contains("*");

                // A category's direct children are the sensor leaves.
                foreach (var leaf in category.Children)
                {
                    if (string.IsNullOrWhiteSpace(leaf.Value)) continue;
                    if (!keepAll && !allowedNames.Contains(leaf.Text)) continue;
                    if (!TryParseValue(leaf.Value, out double value, out string unit)) continue;

                    yield return new Reading
                    {
                        Timestamp = timestamp,
                        Component = componentNode.Text,
                        SensorName = $"{componentNode.Text} / {category.Text} / {leaf.Text}",
                        SensorType = leaf.Type,
                        Value = value,
                        Unit = unit
                    };
                }
            }
        }
    }

    // Depth-first search for hardware nodes whose Text matches a configured component.
    // (Recursive because the matching node sits a couple of levels below the root.)
    private static IEnumerable<LhmNode> FindComponentNodes(
        LhmNode node, IReadOnlyList<ComponentFilter> components)
    {
        if (components.Any(c => c.Match == node.Text))
            yield return node;

        foreach (var child in node.Children)
            foreach (var match in FindComponentNodes(child, components))
                yield return match;
    }

    // "45.3 °C" -> (45.3, "°C").  "12.0 %" -> (12.0, "%").  Handles odd whitespace.
    private static bool TryParseValue(string raw, out double value, out string unit)
    {
        value = 0;
        unit = "";
        var trimmed = raw.Trim();
        // Split on the first space: number on the left, unit on the right.
        int spaceIdx = trimmed.IndexOf(' ');
        string numberPart = spaceIdx < 0 ? trimmed : trimmed[..spaceIdx];
        unit = spaceIdx < 0 ? "" : trimmed[(spaceIdx + 1)..].Trim();

        // LHM uses invariant culture (dot decimal separator) regardless of OS locale.
        return double.TryParse(numberPart, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }
}
