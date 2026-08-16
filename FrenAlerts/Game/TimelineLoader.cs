using FrenAlerts.Engine;

namespace FrenAlerts.Game;

// Loads the shipped timelines and hands out a clock per fight.
//
// Read once at startup, the same as the call pack: a raid night is one process
// and re-reading a file on every zone change is disk work for no gain.
public sealed class TimelineLoader
{
    private readonly IReadOnlyDictionary<ushort, Timeline> _timelines = Load();

    // Whether this fight has a timeline at all, for the status command.
    public bool Has(uint territory) => _timelines.ContainsKey((ushort)territory);

    public int Mechanics(uint territory) =>
        _timelines.TryGetValue((ushort)territory, out var t) ? t.Entries.Count : 0;

    // A fresh clock per fight rather than a reset one, so a pull can never
    // inherit the last one's anchors.
    public TimelineClock? Build(uint territory) =>
        _timelines.TryGetValue((ushort)territory, out var t) ? new TimelineClock(t) : null;

    private static IReadOnlyDictionary<ushort, Timeline> Load()
    {
        try
        {
            var dir = Service.PluginInterface.AssemblyLocation.Directory?.FullName;
            if (dir is null) return new Dictionary<ushort, Timeline>();

            var path = Path.Combine(dir, "timelines.fatime");
            if (!File.Exists(path))
            {
                Service.Log.Warning("Fren Alerts: timelines.fatime missing, no fight will count down.");
                return new Dictionary<ushort, Timeline>();
            }
            return TimelinePack.ReadAll(File.ReadLines(path));
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Fren Alerts: could not read the timelines.");
            return new Dictionary<ushort, Timeline>();
        }
    }
}
