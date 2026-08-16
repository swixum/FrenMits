namespace FrenAlerts.Engine.Alerts;

public static class CallText
{
    public static string Sentence(string text)
    {
        if (text.Length == 0) return text;

        var first = text[0];
        if (!char.IsLetter(first) || char.IsUpper(first)) return text;

        return char.ToUpperInvariant(first) + text[1..];
    }
}
