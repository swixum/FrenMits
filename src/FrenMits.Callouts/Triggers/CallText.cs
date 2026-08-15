using System.Text;

namespace FrenMits.Callouts;

// Fills the tokens a trigger can put in its text.
public static class CallText
{
    public static string Fill(string template, GameEvent e, PlayerContext me)
    {
        if (template.Length == 0 || template.IndexOf('{') < 0) return template;

        var sb = new StringBuilder(template.Length + 16);
        for (var i = 0; i < template.Length; i++)
        {
            if (template[i] != '{') { sb.Append(template[i]); continue; }

            var close = template.IndexOf('}', i);
            if (close < 0) { sb.Append(template[i]); continue; }

            var token = template.Substring(i + 1, close - i - 1);
            sb.Append(Value(token, e, me));
            i = close;
        }
        return sb.ToString();
    }

    private static string Value(string token, GameEvent e, PlayerContext me) => token switch
    {
        "source" => e.Source.Name,
        "target" => e.Target.Name,
        "ability" => e.Name,
        "me" => me.Name,
        "job" => me.Job,
        "slot" => me.Slot,
        _ => "{" + token + "}",
    };
}
