using System.Numerics;
using Dalamud.Interface;
using FrenAlerts.Engine.UserTriggers;
using Dalamud.Bindings.ImGui;

namespace FrenAlerts.Ui;

// The page for triggers somebody wrote themselves.
//
// The engine will run any of the forty-odd fields a trigger can carry, which is far
// more than belongs on one screen. What is here is the part somebody touches while
// they are actually raiding: switch a set on, switch one trigger off, change what it
// says, try it, and paste a code a friend sent.
//
// The deeper fields are still in the file and still run; they are not editable here
// yet, and a trigger that uses them is drawn with a note saying so rather than being
// quietly flattened by an editor that cannot see them.
public partial class ConfigWindow
{
    private string _mineCode = "";
    private string _mineSaid = "";
    private string _mineOpen = "";
    private double _mineSaidAt;

    // The clock the rest of the window counts in, so a note that fades does so at
    // the same speed as everything else and holds still in a paused replay.
    private double MineNow => Runner?.Now ?? 0d;

    private UserTriggerHostView Mine => new(Runner?.Mine);

    // A tiny read-only face onto the host, so the page can be drawn with no runner
    // at all: the window is built before the first frame and drawn during it.
    private readonly record struct UserTriggerHostView(Game.UserTriggerHost? Host)
    {
        public List<UserTriggerSet> Sets => Host?.Sets ?? [];
        public int Live => Host?.Live ?? 0;
        public int Total => Host?.Total ?? 0;
        public int Fired => Host?.Fired ?? 0;
    }

    private void DrawMinePage()
    {
        var mine = Mine;

        if (PageHead("My Triggers", MineLine(mine), C.UserTriggersEnabled,
                reset: null, icon: FontAwesomeIcon.Bolt) is { } on)
        {
            C.UserTriggersEnabled = on;
            C.Save();
        }

        DrawMineShare();

        if (mine.Sets.Count == 0)
        {
            ImGui.Spacing();
            ImGui.TextColored(Theme.V(Theme.Muted),
                "No trigger sets yet. Paste a code above, or start one below.");
        }

        foreach (var set in mine.Sets.ToList()) DrawMineSet(set);

        ImGui.Spacing();
        if (Widgets.AccentButton("New set"))
        {
            mine.Sets.Add(new UserTriggerSet { Name = "My triggers", Category = "Mine" });
            C.Save();
        }

        DrawMineCooldowns();
    }

    // The cooldown tracker: what to watch, and where it sits.
    //
    // On this page rather than its own, because it is the same job as a trigger seen
    // from the other side. A trigger says something once; this shows a number until
    // it is ready.
    private string _cdId = "";
    private bool _cdStatus;

    private void DrawMineCooldowns()
    {
        if (Runner?.Cooldowns is not { } cooldowns) return;

        Widgets.GroupLabel("Cooldown tracker");
        Widgets.ListBegin();

        var on = C.CooldownsEnabled;
        if (Widgets.RowCheck("Show the tracker",
            cooldowns.Entries.Count > 0
                ? $"{cooldowns.Entries.Count} tracked, {cooldowns.Job} equipped"
                : "nothing tracked yet", ref on))
        {
            C.CooldownsEnabled = on;
            C.Save();
        }

        var when = C.CooldownVisibility;
        if (Widgets.RowCombo("Show it", "", ref when, ["Always", "In a duty", "In combat"],
            id: "cdwhen"))
        {
            C.CooldownVisibility = when;
            C.Save();
        }

        var placing = Cooldowns is { Placing: true };
        if (Widgets.RowCheck("Move it", "drag it where you want it", ref placing))
        {
            if (Cooldowns is { } overlay) overlay.Placing = placing;
        }

        // By position as well as by what it is. Two rows for the same thing shared an
        // id, so their switches moved together and Remove took the first of them. Adding
        // one twice is refused now, and a config saved before that still draws right.
        var seat = 0;
        foreach (var entry in cooldowns.Entries.ToList()) DrawCooldownRow(cooldowns, entry, seat++);

        Widgets.RowBegin("Track", _cdStatus ? "a status id" : "an action id", Theme.S(320f));
        ImGui.SetNextItemWidth(Theme.S(110f));
        ImGui.InputTextWithHint("##cdid", "7533", ref _cdId, 12);
        ImGui.SameLine();
        if (ImGui.SmallButton(_cdStatus ? "Status##cdkind" : "Action##cdkind")) _cdStatus = !_cdStatus;
        ImGui.SameLine();
        if (ImGui.SmallButton("Add##cd")) AddCooldown(cooldowns);
        Widgets.RowEnd();

        if (_cdSaid.Length > 0 && MineNow - _cdSaidAt < 12d) Widgets.RowNote(_cdSaid);

        Widgets.ListEnd();
    }

    // Why nothing happened, for twelve seconds under the row that did nothing.
    //
    // Add used to return on a bad id and leave the box exactly as it was, so a typo, a
    // duplicate and a full list all looked identical to a button that had not been
    // pressed hard enough.
    private string _cdSaid = "";
    private double _cdSaidAt;

    private void CdSay(string what)
    {
        _cdSaid = what;
        _cdSaidAt = MineNow;
    }

    private void AddCooldown(Game.Cooldowns cooldowns)
    {
        if (!uint.TryParse(_cdId.Trim(), out var id) || id == 0)
        {
            CdSay("That is not an id. Type the number, like 7533.");
            return;
        }

        if (cooldowns.Board.Tracks(id, _cdStatus))
        {
            CdSay($"Already tracking that {(_cdStatus ? "status" : "action")}.");
            return;
        }

        if (cooldowns.Board.Full)
        {
            CdSay($"That is all {CooldownBoard.MaxEntries} the tracker holds. Remove one first.");
            return;
        }

        cooldowns.Entries.Add(new CooldownEntry
        {
            Id = id,
            IsStatus = _cdStatus,
            // Named and iconed off the client's own sheets, so nobody has to type
            // either. An id with no row keeps the number, which is still trackable.
            Name = NameOfThing(id, _cdStatus),
            IconId = _cdStatus ? Icons.ForStatus(id) : Icons.ForAction(id),
        });
        _cdId = "";
        _cdSaid = "";
        C.Save();
    }

    private void DrawCooldownRow(Game.Cooldowns cooldowns, CooldownEntry entry, int seat)
    {
        ImGui.PushID($"cd{seat}{entry.Id}{entry.IsStatus}");

        var on = entry.Enabled;
        if (Widgets.RowCheck(entry.Name.Length > 0 ? entry.Name : $"{entry.Id}",
            entry.IsStatus ? $"status {entry.Id}" : $"action {entry.Id}", ref on, sub: true))
        {
            entry.Enabled = on;
            C.Save();
        }

        var bar = entry.Style == CooldownStyle.Bar;
        if (Widgets.RowCheck("As a bar", "", ref bar, sub: true))
        {
            entry.Style = bar ? CooldownStyle.Bar : CooldownStyle.Icon;
            C.Save();
        }

        var hide = entry.HideWhenReady;
        if (Widgets.RowCheck("Hide when ready", "", ref hide, sub: true))
        {
            entry.HideWhenReady = hide;
            C.Save();
        }

        Widgets.RowBegin("", "", Theme.S(320f), sub: true);
        if (ImGui.SmallButton("Remove"))
        {
            cooldowns.Entries.Remove(entry);
            C.Save();
        }
        Widgets.RowEnd();

        ImGui.PopID();
    }

    private static string NameOfThing(uint id, bool status)
    {
        try
        {
            return status
                ? Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Status>()
                    ?.GetRowOrDefault(id)?.Name.ExtractText() ?? ""
                : Service.DataManager.GetExcelSheet<Lumina.Excel.Sheets.Action>()
                    ?.GetRowOrDefault(id)?.Name.ExtractText() ?? "";
        }
        catch
        {
            return "";
        }
    }

    private string MineLine(UserTriggerHostView mine)
    {
        if (!C.UserTriggersEnabled) return "Off";
        if (mine.Sets.Count == 0) return "None yet";
        return mine.Fired > 0
            ? $"{mine.Live} of {mine.Total} on, {mine.Fired} said this session"
            : $"{mine.Live} of {mine.Total} on";
    }

    // Paste one in, copy one out. Their codes are a set at a time, which is how they
    // are shared: one fight's worth, not one line.
    private void DrawMineShare()
    {
        Widgets.GroupLabel("Share");
        Widgets.ListBegin();

        Widgets.RowBegin("Code", "paste a set somebody sent you", Theme.S(320f));
        ImGui.SetNextItemWidth(Theme.S(230f));
        ImGui.InputTextWithHint("##minecode", "FASET1:...", ref _mineCode, 8192);
        ImGui.SameLine();
        if (ImGui.Button("Import##mine")) ImportMine();
        Widgets.RowEnd();
        Widgets.ListEnd();

        if (_mineSaid.Length > 0 && MineNow - _mineSaidAt < 12d)
        {
            ImGui.TextColored(
                Theme.V(_mineSaid.StartsWith("Added", StringComparison.Ordinal) ? Theme.Good : Theme.Warn),
                _mineSaid);
        }
    }

    private void ImportMine()
    {
        _mineSaidAt = MineNow;

        if (ShareCode.TryDecode<UserTriggerSet>(ShareCode.SetPrefix, _mineCode, out var set)
            && set is not null)
        {
            // A fresh id, or importing the same code twice would silently replace the
            // copy somebody had already edited.
            set.Id = Guid.NewGuid().ToString("N");
            set.BuiltIn = false;
            Mine.Sets.Add(set);
            C.Save();
            _mineSaid = $"Added \"{set.Name}\", {set.Triggers.Count} triggers.";
            _mineCode = "";
            return;
        }

        if (ShareCode.TryDecode<UserTrigger>(ShareCode.TriggerPrefix, _mineCode, out var one)
            && one is not null)
        {
            one.Id = Guid.NewGuid().ToString("N");
            var into = Mine.Sets.FirstOrDefault(s => !s.BuiltIn)
                       ?? Add(new UserTriggerSet { Name = "Imported", Category = "Mine" });
            into.Triggers.Add(one);
            C.Save();
            _mineSaid = $"Added \"{one.Name}\" to {into.Name}.";
            _mineCode = "";
            return;
        }

        _mineSaid = "That code could not be read.";
    }

    private UserTriggerSet Add(UserTriggerSet set)
    {
        Mine.Sets.Add(set);
        return set;
    }

    private void DrawMineSet(UserTriggerSet set)
    {
        var open = _mineOpen == set.Id;
        var live = set.Triggers.Count(t => t.Enabled);

        Widgets.GroupLabel(set.Name);
        Widgets.ListBegin();

        var enabled = set.Enabled;
        if (Widgets.RowCheck(set.BuiltIn ? $"{set.Name}  (shipped)" : set.Name,
            $"{live} of {set.Triggers.Count} on"
            + (set.Category.Length > 0 ? $", {set.Category}" : ""), ref enabled))
        {
            set.Enabled = enabled;
            C.Save();
        }

        Widgets.RowBegin("", "", Theme.S(320f));
        if (ImGui.SmallButton(open ? $"Hide triggers##{set.Id}" : $"Show triggers##{set.Id}"))
            _mineOpen = open ? "" : set.Id;
        ImGui.SameLine();
        if (ImGui.SmallButton($"Copy code##{set.Id}"))
        {
            ImGui.SetClipboardText(ShareCode.Encode(ShareCode.SetPrefix, set));
            _mineSaid = $"Copied \"{set.Name}\" to the clipboard.";
            _mineSaidAt = MineNow;
        }
        if (!set.BuiltIn)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"Delete##{set.Id}"))
            {
                Mine.Sets.Remove(set);
                C.Save();
                Widgets.RowEnd();
                Widgets.ListEnd();
                return;
            }
        }
        Widgets.RowEnd();
        Widgets.ListEnd();

        if (!open) return;

        foreach (var trigger in set.Triggers.ToList()) DrawMineTrigger(set, trigger);

        if (!set.BuiltIn)
        {
            if (Widgets.AccentButton($"New trigger##{set.Id}"))
            {
                set.Triggers.Add(new UserTrigger { Name = "New trigger" });
                C.Save();
            }
            ImGui.Spacing();
        }
    }

    // What each match kind is called on screen, in the engine's own order so the
    // index the combo hands back is the value.
    private static readonly string[] MatchNames =
    [
        "Anything", "Cast", "Status gained", "Status lost", "Death", "Head marker",
        "Tether", "Chat", "Cast starts", "Ability", "Cast ends", "Fight time",
    ];

    private void DrawMineTrigger(UserTriggerSet set, UserTrigger trigger)
    {
        ImGui.PushID(trigger.Id);
        Widgets.ListBegin();

        var on = trigger.Enabled;
        if (Widgets.RowCheck(trigger.Name, Summary(trigger), ref on))
        {
            trigger.Enabled = on;
            C.Save();
        }

        var name = trigger.Name;
        if (Widgets.RowText("Name", ref name, $"n{trigger.Id}", sub: true))
        {
            trigger.Name = name;
            C.Save();
        }

        var kind = (int)trigger.On;
        if (Widgets.RowCombo("When", "", ref kind, MatchNames, sub: true))
        {
            trigger.On = (TriggerMatch)Math.Clamp(kind, 0, MatchNames.Length - 1);
            C.Save();
        }

        var pattern = trigger.Pattern;
        if (Widgets.RowText("Matches", ref pattern, $"p{trigger.Id}", sub: true))
        {
            trigger.Pattern = pattern;
            C.Save();
        }
        Tip("Cast, status or marker name. Blank = all of them.");

        DrawMinePicker(trigger);

        var mineOnly = trigger.OnlyOnSelf;
        if (Widgets.RowCheck("Only when it is on me", "", ref mineOnly, sub: true))
        {
            trigger.OnlyOnSelf = mineOnly;
            C.Save();
        }

        var says = trigger.Text;
        if (Widgets.RowText("Says", ref says, $"t{trigger.Id}", sub: true))
        {
            trigger.Text = says;
            C.Save();
        }
        Tip("On screen. {player} and {target} fill in as it fires.");

        var speaks = trigger.TtsText;
        if (Widgets.RowText("Reads out", ref speaks, $"s{trigger.Id}", sub: true))
        {
            trigger.TtsText = speaks;
            C.Save();
        }
        Tip("Blank = TTS reads the call text.");

        var seconds = trigger.Duration;
        if (Widgets.RowDrag("On screen for", "seconds", ref seconds, 1f, 20f, "%.0fs", sub: true))
        {
            trigger.Duration = seconds;
            C.Save();
        }

        var withIcon = trigger.ShowIcon;
        if (Widgets.RowCheck("Show an icon", trigger.IconId > 0 ? $"icon {trigger.IconId}" : "none picked yet",
            ref withIcon, sub: true))
        {
            trigger.ShowIcon = withIcon;
            C.Save();
        }
        Tip("Pick a debuff, the icon fills in.");

        var sound = Game.Sounds.Number(trigger.SoundPath);
        if (Widgets.RowDragInt("Sound", sound > 0 ? "the game's own" : "none",
            ref sound, 0, Game.Sounds.Most, sub: true))
        {
            trigger.SoundPath = sound > 0 ? $"se.{sound}" : "";
            C.Save();
        }
        Tip("The same sixteen a macro can play. Zero is silent.");

        if (sound > 0)
        {
            Widgets.RowBegin("", "", Theme.S(320f), sub: true);
            if (ImGui.SmallButton("Hear it")) Game.Sounds.Play(trigger.SoundPath);
            Widgets.RowEnd();
        }
        else if (trigger.SoundPath.Length > 0)
        {
            // An imported trigger can carry a path to a file on somebody else's
            // machine, and a sound that is never going to play should say so rather
            // than look set.
            Widgets.RowNote($"\"{trigger.SoundPath}\" is a file, which this build does not play.");
        }

        var size = trigger.Scale;
        if (Widgets.RowDrag("Size", "against the usual call", ref size, 0.5f, 4f, "%.2fx", sub: true))
        {
            trigger.Scale = size;
            C.Save();
        }

        var ownPlace = trigger.OverridePos;
        if (Widgets.RowCheck("Its own place on screen",
            ownPlace ? $"{trigger.PosX:0.00}, {trigger.PosY:0.00}" : "with the other calls",
            ref ownPlace, sub: true))
        {
            trigger.OverridePos = ownPlace;
            C.Save();
        }
        Tip("Its own spot, out of the stack. Never pushes a fight's call around.");

        if (trigger.OverridePos)
        {
            var x = trigger.PosX;
            if (Widgets.RowDrag("Across", "0 left, 1 right", ref x, 0f, 1f, "%.2f", sub: true))
            {
                trigger.PosX = x;
                C.Save();
            }

            var y = trigger.PosY;
            if (Widgets.RowDrag("Down", "0 top, 1 bottom", ref y, 0f, 1f, "%.2f", sub: true))
            {
                trigger.PosY = y;
                C.Save();
            }
        }

        var colour = trigger.Color;
        if (Widgets.RowColor("Color", "", ref colour, sub: true))
        {
            trigger.Color = colour;
            C.Save();
        }

        Widgets.RowBegin("", "", Theme.S(320f), sub: true);
        if (ImGui.SmallButton($"Try it##{trigger.Id}")) Runner?.Mine.Preview(trigger, Runner.Now);
        ImGui.SameLine();
        if (ImGui.SmallButton($"Copy##{trigger.Id}"))
        {
            ImGui.SetClipboardText(ShareCode.Encode(ShareCode.TriggerPrefix, trigger));
            _mineSaid = $"Copied \"{trigger.Name}\" to the clipboard.";
            _mineSaidAt = MineNow;
        }
        if (!set.BuiltIn)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton($"Remove##{trigger.Id}"))
            {
                set.Triggers.Remove(trigger);
                C.Save();
            }
        }
        Widgets.RowEnd();

        // Said rather than hidden: the fields this page does not draw still run, and
        // an editor that showed only what it understands would look like the rest had
        // been lost.
        if (Deeper(trigger) is { Length: > 0 } more)
            Widgets.RowNote($"Also set: {more}. Those stay as they are.");

        Widgets.ListEnd();
        ImGui.PopID();
    }

    // Pick a cast out of what this fight has actually been seen doing, rather than
    // going hunting for its name.
    //
    // The list is built while standing in the zone, so it is empty until somebody has
    // pulled the boss once. That is worth saying rather than drawing an empty box:
    // an editor offering nothing looks broken, and this one is simply new here.
    private void DrawMinePicker(UserTrigger trigger)
    {
        if (Runner?.Mine is not { } host) return;

        var zone = (ushort)Service.ClientState.TerritoryType;
        var kind = trigger.On switch
        {
            TriggerMatch.StatusGain or TriggerMatch.StatusLose => CatalogKind.Status,
            TriggerMatch.Headmarker => CatalogKind.Headmarker,
            TriggerMatch.Tether => CatalogKind.Tether,
            _ => CatalogKind.Cast,
        };

        var seen = host.Learned(zone, kind);

        Widgets.RowBegin("From this fight", seen.Count > 0
            ? $"{seen.Count} seen here"
            : "nothing seen here yet", Theme.S(320f), sub: true);

        if (seen.Count == 0)
        {
            ImGui.TextColored(Theme.V(Theme.Muted), "pull the boss once");
            Widgets.RowEnd();
            return;
        }

        ImGui.SetNextItemWidth(Theme.S(230f));
        if (ImGui.BeginCombo($"##pick{trigger.Id}", "pick one"))
        {
            // Newest first: the mechanic somebody is writing a trigger for is
            // almost always the one that just happened to them.
            foreach (var entry in seen.Reverse().Take(MaxPicked))
            {
                var label = entry.Name.Length > 0 ? entry.Name : $"{entry.Kind} {entry.Id:X}";
                if (!ImGui.Selectable($"{label}##{entry.Kind}{entry.Id}")) continue;

                trigger.Pattern = entry.Name;
                trigger.DataId = entry.Id;

                // Whatever was picked brings its own art with it, which is the
                // difference between a line of text and a call you recognise before
                // you have read it.
                //
                // Casts were left out because the engine had a picture for a debuff
                // and nothing else. It has ability art now, and the plugin's own calls
                // for the very same cast have been drawing it, so a trigger somebody
                // wrote for that mechanic was the only thing on screen without it.
                //
                // Nothing is filled in for a head marker or a tether on purpose: those
                // are VFX and a line, the game has no sheet row for either, and a
                // number here would be a real picture of something unrelated.
                var art = entry.Kind switch
                {
                    CatalogKind.Status => Icons.ForStatus(entry.Id),
                    CatalogKind.Cast => Icons.ForAction(entry.Id),
                    _ => 0u,
                };
                if (art > 0)
                {
                    trigger.IconId = art;
                    trigger.ShowIcon = true;
                }
                // A name that is still a placeholder cannot be matched on, so a
                // pick without words matches by id instead of quietly never firing.
                trigger.MatchById = entry.Name.Length == 0 || entry.Name.StartsWith('_');
                C.Save();
            }
            ImGui.EndCombo();
        }
        Widgets.RowEnd();
    }

    // As many as a list stays readable at. A long fight learns hundreds, and a combo
    // holding all of them is a scroll nobody finishes.
    private const int MaxPicked = 60;

    private static string Summary(UserTrigger t)
    {
        var kind = MatchNames[Math.Clamp((int)t.On, 0, MatchNames.Length - 1)].ToLowerInvariant();
        var what = t.MatchById && t.DataId != 0 ? $"id {t.DataId:X}"
            : t.Pattern.Length > 0 ? $"\"{t.Pattern}\""
            : "anything";
        return $"{kind}, {what}";
    }

    // Which of the fields this page cannot edit a trigger is actually using.
    private static string Deeper(UserTrigger t)
    {
        var parts = new List<string>();
        if (t.FollowUps.Count > 0) parts.Add($"{t.FollowUps.Count} follow-ups");
        if (t.ClearOn.Enabled) parts.Add("a clear rule");
        if (t.NumConditions.Count + t.VarConditions.Count > 0) parts.Add("conditions");
        if (t.SetVars.Count > 0) parts.Add("variables");
        if (t.DelaySeconds > 0.01f) parts.Add($"a {t.DelaySeconds:0.#}s delay");
        if (!t.AnyZone && t.Zones.Count > 0) parts.Add("a zone list");
        if (t.SoundPath.Length > 0 && !Game.Sounds.Names(t.SoundPath)) parts.Add("a sound file");
        return string.Join(", ", parts);
    }
}
