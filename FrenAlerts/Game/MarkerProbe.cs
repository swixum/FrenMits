using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FrenAlerts.Engine;

namespace FrenAlerts.Game;

public sealed unsafe class MarkerProbe
{
    private const double PollSeconds = 0.1;

    public const int MaxRows = 4000;

    private readonly List<string> _rows = [];
    private readonly Dictionary<uint, (uint Nameplate, ushort StatusVfx)> _last = new(16);

    private double _lastPoll = -99;

    public bool Enabled { get; set; }

    public int Rows => _rows.Count;

    public bool Full { get; private set; }

    public void Poll(double now, uint territory)
    {
        if (!Enabled) return;
        if (!Paced.Due(now, _lastPoll, PollSeconds)) return;
        _lastPoll = now;

        foreach (var obj in Service.ObjectTable)
        {
            if (obj is not IBattleChara actor) continue;
            if (actor.Address == nint.Zero) continue;
            if (!Watchers.Watching(actor.EntityId)) continue;

            var character = (Character*)actor.Address;
            (uint Nameplate, ushort StatusVfx) seen =
                (character->NamePlateIconId, character->StatusLoopVfxId);
            var had = _last.GetValueOrDefault(actor.EntityId);
            if (seen == had) continue;
            _last[actor.EntityId] = seen;

            var who = Who(actor.EntityId);

            if (seen.Nameplate != had.Nameplate)
                Note($"{now:F2}\t{territory}\t{who}\tnameplate\t{seen.Nameplate:X}");
            if (seen.StatusVfx != had.StatusVfx)
                Note($"{now:F2}\t{territory}\t{who}\tstatusvfx\t{seen.StatusVfx:X}");
        }
    }

    public void NoteControl(double now, uint territory, uint category, uint arg1, uint targetId)
    {
        if (!Enabled) return;
        // Party only, same as the rest: a control packet aimed at the boss is not a
        // head marker and would bury the rows that are.
        if (!Watchers.Watching(targetId)) return;

        Note($"{now:F2}\t{territory}\t{Who(targetId)}\tcontrol-{category:X}\t{arg1:X}");
    }

    private void Note(string row)
    {
        if (_rows.Count >= MaxRows)
        {
            Full = true;
            return;
        }
        _rows.Add(row);
    }

    // A position in the party, never a name, so the file stays readable by somebody
    // who was not in that party.
    private static string Who(uint entityId)
    {
        if (PartySlots.Me?.EntityId == entityId) return "me";

        for (var i = 0; i < Service.PartyList.Length; i++)
            if (Service.PartyList[i]?.GameObject?.EntityId == entityId) return $"p{i + 1}";

        // A replay has no party list, and a file of rows all marked "?" cannot say
        // which player a marker landed on, which is the whole question.
        var n = 0;
        foreach (var pc in Watchers.StandingIn())
        {
            n++;
            if (pc.EntityId == entityId) return $"p{n}";
        }
        return "?";
    }

    public string? Write()
    {
        if (_rows.Count == 0) return null;
        try
        {
            var dir = Service.PluginInterface.ConfigDirectory;
            dir.Create();
            var path = Path.Combine(dir.FullName, "markers.tsv");
            using var to = new StreamWriter(path, append: true);
            to.WriteLine("# time\tterritory\tslot\tfield\tvalue");
            foreach (var row in _rows) to.WriteLine(row);
            return path;
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Fren Alerts: could not write the marker probe file.");
            return null;
        }
    }

    public void Forget()
    {
        _rows.Clear();
        _last.Clear();
        Full = false;
        _lastPoll = -99;
    }
}
