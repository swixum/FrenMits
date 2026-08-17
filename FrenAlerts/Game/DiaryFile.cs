namespace FrenAlerts.Game;

// Where a pull's recording ends up, and how much of it is kept.
public static class DiaryFile
{
    // Enough for a night of pulls, small enough to still open. Past it the file
    // starts over rather than appending, because a recorder left on by accident is
    // otherwise a disk that fills while nobody is looking.
    public const long MaxBytes = 8 * 1024 * 1024;

    public const string Name = "pulls.log";

    public static string? Write(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        try
        {
            var dir = Service.PluginInterface.ConfigDirectory;
            dir.Create();
            var path = Path.Combine(dir.FullName, Name);

            var file = new FileInfo(path);
            var append = !file.Exists || file.Length < MaxBytes;

            using var to = new StreamWriter(path, append);
            if (!append) to.WriteLine("# started over: the file had reached its limit.");
            to.WriteLine();
            to.WriteLine($"==== {DateTime.Now:yyyy-MM-dd HH:mm:ss} ====");
            to.WriteLine(text);
            return path;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Fren Alerts: could not write the pull recording.");
            return null;
        }
    }
}
