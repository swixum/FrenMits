using FrenAlerts.Engine;
using FrenAlerts.Engine.UserTriggers;

namespace FrenAlerts.Game;

// Triggers somebody wrote themselves, connected to this game.
//
// The matcher is the engine's and knows nothing about a client: events in, calls
// out. This is the half that only a running game can answer. What the event was
// called, whose it was, what role they are, how much health they have left. Their
// own editor asks all four, and a trigger written against it is only portable if
// every one of them means the same thing here.
//
// Names are the interesting part. A trigger matches "Ceruleum Vent", and the event
// carries 0xB384, so the words have to come from somewhere: the client's own sheets
// normally, and the shipped name files for a fight new enough that the client is
// still hiding them behind placeholders.
public sealed class UserTriggerHost
{
    // Looked up once each. A fight is a few dozen distinct casts and statuses
    // repeated hundreds of times, so this is the difference between two sheet reads
    // a pull and two thousand.
    private const int MaxNames = 4096;

    // How often what a zone has been seen doing is written out. A pull adds a few
    // dozen entries and a raid night is hours, so this is little and often rather
    // than one write at the end that a crash loses.
    private const double SavePace = 60.0;

    private readonly UserTriggerEngine _engine;
    private readonly GameWorld _world = new();
    private readonly NameCatalog _catalog = new();
    private readonly Dictionary<(CatalogKind Kind, uint Id), string> _names = new();

    // Everything this zone has been seen to do, so somebody writing a trigger picks
    // a cast off a list instead of hunting for its id.
    private readonly LearnedCatalog _learned = new();

    private double _lastSave = -99;

    public UserTriggerHost()
    {
        _engine = new UserTriggerEngine(_world);
        _engine.Say = call => Say?.Invoke(call);
    }

    // Where a finished call goes, left to the host: what a call means on screen is
    // not this file's question.
    public Action<UserCall>? Say { get; set; }

    public List<UserTriggerSet> Sets => _engine.Sets;

    public int Fired => _engine.Fired;

    // How many are switched on, which is the number worth showing: a set that is off
    // is not a trigger that is quiet, it is one that is not there at all.
    public int Live => _engine.Sets.Where(s => s.Enabled).Sum(s => s.Triggers.Count(t => t.Enabled));

    public int Total => _engine.Sets.Sum(s => s.Triggers.Count);

    // The names shipped for fights the client is still hiding, for the editor to
    // offer and for a trigger to match on.
    public int NamesKnown => _catalog.Count;

    public NameCatalog Catalog => _catalog;

    // Reads the shipped name files. On a frame rather than in a constructor, for the
    // same reason their fights are: this opens files.
    public void Load()
    {
        var dir = Service.PluginInterface.AssemblyLocation.Directory?.FullName;
        if (dir is null) return;

        try { _catalog.Load(Path.Combine(dir, "names")); }
        catch (Exception ex) { Service.Log.Warning($"Fren Alerts: no shipped names, {ex.Message}"); }

        // What this machine has already watched happen. A file that will not read is
        // dropped rather than repaired: it is a convenience list that rebuilds itself
        // from the next pull.
        try { _learned.Load(LearnedPath); }
        catch (Exception ex) { Service.Log.Warning($"Fren Alerts: no learned list, {ex.Message}"); }

        Service.Log.Information(
            $"Fren Alerts: {_catalog.Count} shipped names, "
            + $"{_learned.Zones} zones already watched.");
    }

    // What somebody has saved, plus whatever the shipped sets have gained since they
    // last ran. Topped up rather than overwritten: a built-in set that somebody has
    // switched on and edited stays as they left it.
    public void Use(IEnumerable<UserTriggerSet> saved, int revision, out int nowAt)
    {
        nowAt = BuiltInTriggers.Revision;
        _engine.Sets.Clear();
        _engine.Sets.AddRange(TriggerSetup.TopUp(saved, BuiltInTriggers.Build(), revision));
    }

    // Who you are and who everybody else is, read on the party poll rather than per
    // event: it walks eight members and the answer changes about once a pull.
    public void Refresh(ushort territory, uint you, PartyContext party)
    {
        _world.Territory = territory;
        _world.You = you;
        _world.Party = party;
    }

    // What this zone has been seen doing, for the editor to offer.
    public IReadOnlyList<CatalogEntry> Learned(ushort zone, CatalogKind kind) =>
        _learned.For(zone, kind);

    public int LearnedZones => _learned.Zones;

    // One event, in the shape a hand-written trigger asks its questions in.
    public void Feed(in GameEvent e)
    {
        if (Kind(e.Kind) is not { } kind) return;

        var carried = new TriggerEvent
        {
            Kind = kind,
            Time = e.Time,
            Name = NameOf(kind, e.Id),
            DataId = e.DataId,
            SourceId = e.SourceId,
            SourceName = Who(e.SourceId),
            SourceSide = Side(e.SourceId),
            TargetId = e.TargetId,
            TargetName = Who(e.TargetId),
            TargetSide = Side(e.TargetId),
            // Resolved through the game's own sheet, never carried across raw.
            //
            // This used to hand the head marker or tether id straight over as an icon
            // number. Those are small numbers and the icon sheet has rows there, so it
            // drew a real picture of something unrelated, and it won that fight against
            // the icon somebody had picked on the row: Fire lands on you, the trigger is
            // set to show the Fire debuff, and the call comes up wearing whatever art
            // sits at row 218. Neither markers nor tethers have art in the sheet, so
            // there is nothing to put here for them.
            IconId = e.Kind == EventKind.StatusGain ? Ui.Icons.ForStatus(e.Id) : 0,
            Value = e.Kind == EventKind.CastStart ? e.CastTime : e.Duration,
            Count = e.Param,
            Category = e.Kind == EventKind.ActorControl ? e.Id : 0,
            Param1 = e.Arg1,
            Param2 = e.Arg2,
        };

        _engine.Handle(carried);

        // Remembered whether or not anything matched: the list is what the fight did,
        // not what somebody has already written a trigger for. Their own filter comes
        // with it, or one pull of party buffs buries everything worth picking.
        _learned.Record(_world.Territory, carried, carried.SourceSide == ActorSide.Enemy);
    }

    public void Tick(double now)
    {
        _engine.Tick(now);

        // Written little and often. One write at the end of the night is one crash
        // away from a night of nothing.
        if (_learned.Dirty && Paced.Due(now, _lastSave, SavePace))
        {
            _lastSave = now;
            Remember();
        }
    }

    // Where the learned list is kept: beside the config, because it is this
    // machine's own record of the fights it has stood in.
    private static string LearnedPath =>
        Path.Combine(Service.PluginInterface.ConfigDirectory.FullName, "learned.json");

    public void Remember()
    {
        try
        {
            Service.PluginInterface.ConfigDirectory.Create();
            _learned.Save(LearnedPath);
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"Fren Alerts: the learned list could not be written, {ex.Message}");
        }
    }

    // Said out loud with nothing happening, for somebody editing one. Theirs, so the
    // sample is exactly what a real fire would look like.
    public void Preview(UserTrigger trigger, double now) => _engine.Preview(trigger, now);

    // The colour the trigger asked for, packed the way the screen wants it, or zero
    // where it is the default. Looked up rather than carried on the call, because
    // what a call looks like is the trigger's setting rather than the fire's.
    public uint TintOf(string ownerId) => LookOf(ownerId).Tint;

    // How a trigger asked its call to look: its colour, its size, and where it goes
    // if it named a place of its own.
    //
    // Looked up rather than carried on the call, because how a call looks is the
    // trigger's setting rather than something the fire decides. Their editor offers
    // all three and every one of them was being dropped on the way to the screen.
    public (uint Tint, float Scale, System.Numerics.Vector2? At) LookOf(string ownerId)
    {
        foreach (var set in _engine.Sets)
        {
            foreach (var trigger in set.Triggers)
            {
                if (trigger.Id != ownerId) continue;

                // Their scale is against a call at their own default size, so a two
                // is a normal call rather than a call at twice the setting.
                var scale = trigger.Scale <= 0.01f ? 1f : trigger.Scale / 2f;

                return (TriggerSetup.Packed(trigger.Color), scale,
                    trigger.OverridePos
                        ? new System.Numerics.Vector2(trigger.PosX, trigger.PosY)
                        : null);
            }
        }

        return (0, 1f, null);
    }

    // A pull ending or a zone changing. Everything a trigger was waiting on describes
    // a stretch of a fight that is over.
    public void Reset()
    {
        _engine.Reset();
        _names.Clear();
    }

    // A call of theirs is on screen or is not, which is what a trigger set to wait
    // waits for.
    public void NoteLive(string ownerId, bool live) => _engine.NoteLive(ownerId, live);

    private static TriggerEventKind? Kind(EventKind kind) => kind switch
    {
        EventKind.CastStart => TriggerEventKind.CastStart,
        EventKind.AbilityHit => TriggerEventKind.Ability,
        EventKind.StatusGain => TriggerEventKind.StatusGain,
        EventKind.StatusLose => TriggerEventKind.StatusLose,
        EventKind.HeadMarker => TriggerEventKind.Headmarker,
        EventKind.Tether => TriggerEventKind.Tether,
        EventKind.ActorSpawn => TriggerEventKind.Added,
        EventKind.ActorControl => TriggerEventKind.ActorControl,
        EventKind.MapEffect => TriggerEventKind.MapEffect,
        _ => null,
    };

    // What the thing is called: the client's own sheet where it has the words, and
    // the shipped file where a fight is new enough that it does not.
    private string NameOf(TriggerEventKind kind, uint id)
    {
        if (id == 0) return "";

        var table = kind is TriggerEventKind.StatusGain or TriggerEventKind.StatusLose
            ? CatalogKind.Status
            : CatalogKind.Cast;

        if (_names.TryGetValue((table, id), out var known)) return known;
        if (_names.Count >= MaxNames) return "";

        var name = FromSheet(table, id);
        if (name.Length == 0 || name.StartsWith('_')) name = _catalog.Of(table, id) ?? name;

        _names[(table, id)] = name;
        return name;
    }

    private static string FromSheet(CatalogKind table, uint id)
    {
        try
        {
            return table == CatalogKind.Status
                ? Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()?.GetRowOrDefault(id)?.Name.ExtractText() ?? ""
                : Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()?.GetRowOrDefault(id)?.Name.ExtractText() ?? "";
        }
        catch
        {
            // A row that is not there is normal: half the ids a fight carries are
            // not in either sheet at all.
            return "";
        }
    }

    private static string Who(uint id) =>
        id == 0 ? "" : Service.ObjectTable.SearchByEntityId(id)?.Name.TextValue ?? "";

    private static ActorSide Side(uint id)
    {
        if (id == 0) return ActorSide.Other;
        if (PartySlots.Me?.EntityId == id) return ActorSide.You;
        if (Watchers.Watching(id)) return ActorSide.Party;
        return ActorSide.Enemy;
    }

    // What the matcher cannot know for itself, answered off the client.
    private sealed class GameWorld : ITriggerWorld
    {
        public ushort Territory { get; set; }

        public uint You { get; set; }

        public PartyContext? Party { get; set; }

        public RoleFilter YourRole => RoleOf(You);

        public RoleFilter RoleOf(uint actorId) =>
            Party?.RoleOf(actorId) switch
            {
                "tank" => RoleFilter.Tank,
                "healer" => RoleFilter.Healer,
                "dps" => RoleFilter.Dps,
                _ => RoleFilter.Any,
            };

        // Below zero where nothing knows, which is their own rule: a health
        // condition is skipped rather than guessed.
        public float HealthPercent(uint actorId)
        {
            if (Service.ObjectTable.SearchByEntityId(actorId)
                is not Dalamud.Game.ClientState.Objects.Types.IBattleChara c) return -1f;

            return c.MaxHp == 0 ? -1f : c.CurrentHp * 100f / c.MaxHp;
        }

        // Already carrying it, which is a different question from it just arriving:
        // a follow-up armed after the debuff landed still has to resolve.
        //
        // Walked rather than queried, because the status list is a fixed span the
        // client owns and a query over it allocates on every check.
        public bool HasStatus(uint actorId, uint statusId, string namePart)
        {
            if (Service.ObjectTable.SearchByEntityId(actorId)
                is not Dalamud.Game.ClientState.Objects.Types.IBattleChara c) return false;

            foreach (var status in c.StatusList)
            {
                if (status is null || status.StatusId == 0) continue;
                if (statusId != 0 && status.StatusId != statusId) continue;
                if (namePart.Length > 0
                    && !(status.GameData.ValueNullable?.Name.ExtractText() ?? "")
                        .Contains(namePart, StringComparison.OrdinalIgnoreCase)) continue;

                return true;
            }

            return false;
        }

        public string JobOf(uint actorId) =>
            Service.ObjectTable.SearchByEntityId(actorId)
                is Dalamud.Game.ClientState.Objects.Types.IBattleChara c
                ? c.ClassJob.ValueNullable?.Abbreviation.ExtractText() ?? ""
                : "";
    }
}
