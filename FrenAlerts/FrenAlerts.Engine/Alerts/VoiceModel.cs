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

    private readonly Func<string, long> _sizeOf;

    // Takes a way to measure a file rather than a folder, so this can be checked
    // against anything: a real directory, or a table in a test.
    public VoiceModel(Func<string, long> sizeOf) => _sizeOf = sizeOf;

    public IEnumerable<Piece> Missing =>
        Needed.Where(p => !p.LooksRight(Safely(p.Name)));

    public bool Ready => !Missing.Any();

    public long BytesToFetch => Missing.Sum(p => p.Bytes);

    // What to tell somebody who asked why it is not talking.
    public string Describe()
    {
        var missing = Missing.ToList();
        if (missing.Count == 0) return "Local voice is installed and ready.";

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
}
