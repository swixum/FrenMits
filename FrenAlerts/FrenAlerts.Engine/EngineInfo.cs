namespace FrenAlerts.Engine;

// The status command prints this, which is what proves the engine assembly
// actually shipped in the zip and loaded, rather than failing at the first call.
public static class EngineInfo
{
    public static string Version =>
        typeof(EngineInfo).Assembly.GetName().Version?.ToString() ?? "unknown";
}
