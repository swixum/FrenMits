namespace FrenAlerts.Engine.Alerts;

public sealed class VoiceModel
{
    public readonly record struct Piece(string Name, long Bytes)
    {
        // Zero bytes means "it only has to be there".
        //
        // Our own executable is rebuilt whenever its code changes, so its size is not
        // a fact about a correct download: pinning it would fail the whole pack the
        // first time the helper is edited. The pieces that come from a package do not
        // move, and those are the ones a truncated download would cut short.
        public bool LooksRight(long actual) =>
            actual > 0 && (Bytes <= 0 || Math.Abs(actual - Bytes) <= Bytes / 100);
    }

    public static readonly Piece[] Needed =
    [
        new("FrenAlertsVoice.exe", 0),
        new("MisakiSharp.dll", 69_382_656),
        new("onnxruntime.dll", 12_418_080),
        new("NumSharp.dll", 3_622_912),
        new("Microsoft.ML.OnnxRuntime.dll", 201_776),
        new("KokoroSharp.dll", 101_888),
    ];

    public static long TotalBytes => Needed.Sum(p => p.Bytes);

    public const long PackBytes = 169_000_000;

    // The voices live in their own folder beside the pieces. They are checked apart
    // from the list above because there are 28 of them, any one is enough to speak,
    // and which ones are there is what the picker is built from.
    public const string VoicesFolder = "voices";

    private readonly Func<string, long> _sizeOf;
    private readonly Func<IEnumerable<string>> _voiceFiles;

    // Takes a way to measure a file rather than a folder, so this can be checked
    // against anything: a real directory, or a table in a test.
    public VoiceModel(Func<string, long> sizeOf, Func<IEnumerable<string>>? voiceFiles = null)
    {
        _sizeOf = sizeOf;
        _voiceFiles = voiceFiles ?? Array.Empty<string>;
    }

    // Reads a real folder, which is what the plugin does; the tests hand it tables.
    public static VoiceModel ForFolder(string folder) => new(
        name =>
        {
            var file = new FileInfo(Path.Combine(folder, name));
            return file.Exists ? file.Length : 0;
        },
        () =>
        {
            var dir = Path.Combine(folder, VoicesFolder);
            return Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "*.npy") : [];
        });

    public IEnumerable<Piece> Missing =>
        Needed.Where(p => !p.LooksRight(Safely(p.Name)));

    public IReadOnlyList<VoiceCatalog.Choice> Voices => VoiceCatalog.Read(VoiceFiles());

    // A pack with every file and no voices loads nothing and dies on startup, so it
    // is not ready however complete the list above looks.
    public bool Ready => !Missing.Any() && Voices.Count > 0;

    public long BytesToFetch => Missing.Sum(p => p.Bytes);

    // What to tell somebody who asked why it is not talking.
    public string Describe()
    {
        var missing = Missing.ToList();
        if (missing.Count == 0)
            return Voices.Count > 0
                ? $"Local voice is installed and ready, {Voices.Count} voices."
                : $"Local voice has no voices: the {VoicesFolder} folder is missing, " +
                  "so it cannot start.";

        // Whole pack or a broken one, said differently: the first is a choice not
        // yet made and the second is something that went wrong.
        if (missing.Count == Needed.Length)
            return $"Local voice is not installed. It is a {PackBytes / 1_048_576}MB download, " +
                   "plus the model it fetches on first run. The system voice needs nothing.";

        return $"Local voice is incomplete: {missing.Count} of {Needed.Length} files, " +
               $"{BytesToFetch / 1_048_576}MB to fetch. Missing " +
               string.Join(", ", missing.Select(p => p.Name)) + ".";
    }

    private long Safely(string name)
    {
        try
        {
            return _sizeOf(name);
        }
        catch
        {
            return 0;
        }
    }

    private IEnumerable<string> VoiceFiles()
    {
        try
        {
            return _voiceFiles().ToList();
        }
        catch
        {
            return [];
        }
    }
}
