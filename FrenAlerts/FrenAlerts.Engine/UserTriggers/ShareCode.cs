using System.IO.Compression;
using System.Text;
using System.Text.Json;

namespace FrenAlerts.Engine.UserTriggers;

// A trigger or a set of them, as a string somebody can paste into a chat window.
//
// Same three steps as theirs and in the same order, because that is what makes a
// code readable at all: the thing as JSON, gzipped, then base64, behind a short
// prefix that says which of the two it is. A code that unzips to the wrong shape
// fails as a false rather than as a plugin falling over, which matters when the
// input is whatever was on the clipboard.
//
// The prefixes are ours. Theirs carry their plugin's name, and a name is the one
// thing that does not travel.
public static class ShareCode
{
    public const string SetPrefix = "FASET1:";

    public const string TriggerPrefix = "FATRG1:";

    private static readonly JsonSerializerOptions Json = new()
    {
        IncludeFields = true,
        WriteIndented = false,
    };

    public static string Encode<T>(string prefix, T value)
    {
        var bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json));

        using var packed = new MemoryStream();
        using (var zip = new GZipStream(packed, CompressionLevel.Optimal, leaveOpen: true))
            zip.Write(bytes, 0, bytes.Length);

        return prefix + Convert.ToBase64String(packed.ToArray());
    }

    public static bool TryDecode<T>(string prefix, string code, out T? value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(code)) return false;

        code = code.Trim();
        if (code.StartsWith(prefix, StringComparison.Ordinal)) code = code[prefix.Length..];

        try
        {
            using var packed = new MemoryStream(Convert.FromBase64String(code));
            using var zip = new GZipStream(packed, CompressionMode.Decompress);
            using var plain = new MemoryStream();
            zip.CopyTo(plain);

            value = JsonSerializer.Deserialize<T>(Encoding.UTF8.GetString(plain.ToArray()), Json);
            return value is not null;
        }
        catch
        {
            return false;
        }
    }

    public static string Share(UserTriggerSet set) => Encode(SetPrefix, set);

    public static string Share(UserTrigger trigger) => Encode(TriggerPrefix, trigger);

    // Which of the two a pasted code is, without trying both.
    public static bool IsSet(string code) =>
        code?.TrimStart().StartsWith(SetPrefix, StringComparison.Ordinal) ?? false;

    public static bool IsTrigger(string code) =>
        code?.TrimStart().StartsWith(TriggerPrefix, StringComparison.Ordinal) ?? false;
}
