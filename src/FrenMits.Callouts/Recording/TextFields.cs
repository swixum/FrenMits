using System.Text;

namespace FrenMits.Callouts;

// Tab separated files, shared by the recording and the trigger pack. Names come
// from the game and text comes from a bake, so both still have to survive.
internal static class TextFields
{
    public const char Sep = '\t';

    public static string Escape(string s)
    {
        if (s.Length == 0) return s;
        var sb = new StringBuilder(s.Length + 4);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append("\\\\"); break;
                case '\t': sb.Append("\\t"); break;
                case '\n': sb.Append("\\n"); break;
                case '\r': sb.Append("\\r"); break;
                default: sb.Append(c); break;
            }
        }
        return sb.ToString();
    }

    public static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (var i = 0; i < s.Length; i++)
        {
            if (s[i] != '\\' || i + 1 >= s.Length) { sb.Append(s[i]); continue; }
            switch (s[++i])
            {
                case 't': sb.Append('\t'); break;
                case 'n': sb.Append('\n'); break;
                case 'r': sb.Append('\r'); break;
                case '\\': sb.Append('\\'); break;
                default: sb.Append(s[i]); break;
            }
        }
        return sb.ToString();
    }
}
