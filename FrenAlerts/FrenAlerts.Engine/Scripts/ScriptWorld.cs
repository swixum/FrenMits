using System.Globalization;
using System.Text;
using Jint;

namespace FrenAlerts.Engine.Scripts;

// The reads their scripts make of the world around them.
//
// Their fight files do not only match events. Several of their calls ask what is
// standing where at the moment the call is made: which of the four adds is alive,
// where the boss is facing, which tower is nearest. Their prelude expects the host
// to supply three functions for that, and without them every one of those calls
// returns an empty list and says nothing, quietly, with no error anywhere.
//
// The names are theirs and are not free to change: `__actorsByBase`, which their
// `actorsByBase(csv)` wraps, `__actorsAllInfo`, which their `actorsAll()` parses,
// and `__log`, where their own console output goes.
public sealed class ScriptWorld(ActorBook book)
{
    // Their per-fight state carries the last place every actor was seen, and their
    // fight kit falls back to it whenever the event itself carried no position.
    public const string PositionsField = "actorPositions";

    // Where the waymarks are, for the one fight that reads them: Dancing Mad works
    // out which limit cut number started where by asking which waymark the first spot
    // was nearest. Empty until the game side fills it in, which reads as "no answer"
    // rather than as the middle of the arena.
    public List<Waymark> Waymarks { get; } = [];

    public void Bind(Jint.Engine js, Action<string>? log = null)
    {
        js.SetValue("__actorsByBase", ByBase);
        js.SetValue("__actorsAllInfo", AllInfo);
        js.SetValue("__log", log ?? (_ => { }));
        js.SetValue("__lcWaymarks", WaymarkRows);
    }

    // Their shape: one row per active waymark, slot index then x then z.
    public double[][] WaymarkRows()
    {
        var rows = new double[Waymarks.Count][];
        for (var i = 0; i < Waymarks.Count; i++) rows[i] = Waymarks[i].AsRow();
        return rows;
    }

    // Every actor of the kinds asked for, as their prelude reads them: base id,
    // where it is, and which way it is facing.
    public double[][] ByBase(string csv)
    {
        var rows = new List<double[]>();
        if (string.IsNullOrWhiteSpace(csv)) return [.. rows];

        foreach (var part in csv.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var id = ReadId(part.Trim());
            if (id == 0) continue;

            foreach (var actor in book.OfKind(id))
                rows.Add([actor.DataId, actor.Where.X, actor.Where.Y, actor.Where.Heading]);
        }

        return [.. rows];
    }

    // Their own shape, as JSON, because that is what their `actorsAll()` parses.
    public string AllInfo()
    {
        var json = new StringBuilder("[");
        var first = true;

        foreach (var actor in book.Placed())
        {
            if (!first) json.Append(',');
            first = false;
            json.Append("{\"base\":").Append(Num(actor.DataId))
                .Append(",\"x\":").Append(Num(actor.Where.X))
                .Append(",\"y\":").Append(Num(actor.Where.Y))
                .Append(",\"h\":").Append(Num(actor.Where.Heading))
                .Append(",\"hp\":").Append(Num(actor.MaxHp))
                .Append(",\"n\":\"").Append(Escape(actor.Name)).Append("\"}");
        }

        return json.Append(']').ToString();
    }

    // Where everything was last seen, written into their state under the key their
    // fight kit reads. Rebuilt rather than added to, so an actor that despawned
    // cannot answer a question about this pull's geometry.
    public void Remember(Jint.Engine js)
    {
        var table = new StringBuilder("__data.").Append(PositionsField).Append(" = {");
        var first = true;

        foreach (var actor in book.Placed())
        {
            if (!first) table.Append(',');
            first = false;
            table.Append('"').Append(actor.Id.ToString("X8", CultureInfo.InvariantCulture))
                 .Append("\":{x:").Append(Num(actor.Where.X))
                 .Append(",y:").Append(Num(actor.Where.Y))
                 .Append(",heading:").Append(Num(actor.Where.Heading)).Append('}');
        }

        js.Execute(table.Append("};").ToString());
    }

    // Their csv takes either form, because their own files write both.
    private static uint ReadId(string text)
    {
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return uint.TryParse(text[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var hex)
                ? hex : 0;

        return uint.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dec) ? dec : 0;
    }

    private static string Num(double value) =>
        double.IsNaN(value) ? "0" : value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Escape(string name) =>
        name.Replace("\\", "\\\\").Replace("\"", "\\\"");
}
