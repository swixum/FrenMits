using System.Globalization;
using System.Text.RegularExpressions;

namespace FrenAlerts.Engine.UserTriggers;

// One call a user trigger wants made.
public sealed record UserCall(
    string OwnerId, string Text, string Speech, float Seconds, bool Countdown, uint IconId,
    string SoundPath = "")
{
    public bool ClearsOwner { get; init; }
}

// Their user-trigger engine, ported.
//
// The rules are all theirs and the interesting ones are the ones nobody would guess:
// a trigger with no cooldown of its own still gets two and a half seconds, a trigger
// set to wait will not fire again while its own call is still on screen, variables
// are compared as text unless the condition says numeric, and a follow-up armed on
// an event fires the moment its conditions are all met rather than when the timer
// runs out.
//
// Pure, and driven from outside: events in, calls out, a clock handed in. What it
// cannot know about the world it asks the world for.
public sealed class UserTriggerEngine(ITriggerWorld? world = null)
{
    // A trigger that names no cooldown still gets this one, because the same cast
    // arrives once per target and nobody wants it eight times.
    private const double DefaultCooldown = 2.5;

    private readonly ITriggerWorld _world = world ?? new NoWorld();
    private readonly Dictionary<string, Regex> _patterns = new(StringComparer.Ordinal);
    private readonly Dictionary<string, double> _lastFire = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _vars = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<(double Due, UserTrigger Trigger, TriggerEvent Event)> _waiting = [];
    private readonly List<(double Due, FollowUp Step, TriggerEvent Context)> _waitingSteps = [];
    private readonly List<ArmedFollow> _armed = [];
    private readonly List<(double Expiry, UserTrigger Trigger)> _clearWatch = [];
    private readonly HashSet<string> _live = new(StringComparer.Ordinal);
    private readonly HashSet<string> _firedAtTime = new(StringComparer.Ordinal);
    private double? _pullStart;

    private static readonly Regex CaptureToken = new(@"\$\{(\w+)\}", RegexOptions.Compiled);
    private static readonly Regex VarToken = new(@"\{\$(\w+)\}", RegexOptions.Compiled);

    public List<UserTriggerSet> Sets { get; } = [];

    public Action<UserCall>? Say;

    public int Fired { get; private set; }

    public IReadOnlyDictionary<string, string> Vars => _vars;

    // A call of this trigger's is still on screen, which is what "wait" waits for.
    // Told rather than watched, because what is on screen is the host's business.
    public void NoteLive(string ownerId, bool live)
    {
        if (live) _live.Add(ownerId);
        else _live.Remove(ownerId);
    }

    public void Reset()
    {
        _waiting.Clear();
        _waitingSteps.Clear();
        _armed.Clear();
        _clearWatch.Clear();
        _lastFire.Clear();
        _live.Clear();
        _vars.Clear();
        _firedAtTime.Clear();
        _pullStart = null;
    }

    // A trigger can be set to fire at a time rather than at an event: forty seconds
    // in, whatever has happened. That needs a moment the pull started from, and the
    // clock only exists between these two.
    public void BeginFight(double now)
    {
        _pullStart = now;
        _firedAtTime.Clear();
    }

    public void EndFight()
    {
        _pullStart = null;
        _firedAtTime.Clear();
    }

    public double? FightTime(double now) => _pullStart is { } start ? now - start : null;

    // Once each per pull, and never before the pull started: a countdown that fires
    // twice is worse than one that fires late.
    private void TickFightTime(double now)
    {
        if (_pullStart is not { } start) return;

        var elapsed = (float)(now - start);

        foreach (var set in Sets)
        {
            if (!set.Enabled) continue;

            foreach (var trigger in set.Triggers)
            {
                if (!trigger.Enabled || trigger.On != TriggerMatch.FightTime) continue;
                if (elapsed < trigger.FightTime || !_firedAtTime.Add(trigger.Id)) continue;
                if (!trigger.AnyZone && trigger.Zones.Count > 0
                    && !trigger.Zones.Contains(_world.Territory)) continue;
                if (!SelfRoleMatches(trigger.SelfRoles)) continue;

                Fire(trigger, new TriggerEvent { Kind = TriggerEventKind.Ability, Time = now });
            }
        }
    }

    // A trigger said out loud with nothing happening, for somebody editing one. Their
    // own sample: the pattern stands in for the words the event would have carried.
    public void Preview(UserTrigger t, double now = 0)
    {
        var sample = new TriggerEvent
        {
            Kind = TriggerEventKind.CastStart,
            Time = now,
            Name = string.IsNullOrEmpty(t.Pattern) ? "Sample" : t.Pattern,
        };

        var text = t.TextEnabled
            ? Substitute(string.IsNullOrWhiteSpace(t.Text) ? t.Name : t.Text, sample, null)
            : "";
        var speech = t.TtsEnabled
            ? Substitute(string.IsNullOrWhiteSpace(t.TtsText) ? t.Text : t.TtsText, sample, null)
            : "";

        Say?.Invoke(new UserCall(
            t.Id, text, speech, t.Duration, t.UseEventDuration && t.ShowCountdown,
            t.ShowIcon ? t.IconId : 0, t.SoundPath));
    }

    public void Handle(TriggerEvent e)
    {
        AdvanceArmed(e);
        WatchForClears(e);

        foreach (var set in Sets)
        {
            if (!set.Enabled) continue;

            foreach (var trigger in set.Triggers)
            {
                if (!trigger.Enabled || !Matches(trigger, e)) continue;

                var cooldown = trigger.Cooldown > 0.01f ? trigger.Cooldown : DefaultCooldown;
                if (_lastFire.TryGetValue(trigger.Id, out var last) && e.Time - last < cooldown) continue;
                if (ModeOf(trigger) == Concurrency.Wait && _live.Contains(trigger.Id)) continue;

                _lastFire[trigger.Id] = e.Time;

                var caps = Capture(trigger, e);
                ApplyVars(trigger, e, caps);

                if (trigger.DelaySeconds > 0.01f) _waiting.Add((e.Time + trigger.DelaySeconds, trigger, e));
                else Fire(trigger, e, caps);

                if (trigger.ClearOn.Enabled)
                    _clearWatch.Add((e.Time + Math.Max(0.5f, trigger.ClearOn.Seconds), trigger));
            }
        }
    }

    public void Fire(UserTrigger t, TriggerEvent e) => Fire(t, e, Capture(t, e));

    public void Fire(UserTrigger t, TriggerEvent e, IReadOnlyDictionary<string, string>? caps)
    {
        var seconds = t.UseEventDuration && e.Value > 0.1f ? e.Value : t.Duration;
        var icon = t.ShowIcon ? (e.IconId != 0 ? e.IconId : t.IconId) : 0;

        var text = t.TextEnabled && !string.IsNullOrWhiteSpace(t.Text) ? Substitute(t.Text, e, caps) : "";
        var speech = t.TtsEnabled
            ? Substitute(string.IsNullOrWhiteSpace(t.TtsText) ? t.Text : t.TtsText, e, caps)
            : "";

        if (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(speech))
        {
            Fired++;
            Say?.Invoke(new UserCall(
                t.Id, text, speech, seconds, t.UseEventDuration && t.ShowCountdown, icon, t.SoundPath)
            {
                // Replace takes the trigger's own call off first, which is how a
                // count that ticks up reads as one line rather than five.
                ClearsOwner = ModeOf(t) == Concurrency.Replace,
            });
        }

        foreach (var step in t.FollowUps)
        {
            if (step.On == FollowUpOn.Timer)
            {
                _waitingSteps.Add((e.Time + Math.Max(0f, step.Seconds), step, e));
                continue;
            }

            step.EnsureConditions();
            var armed = new ArmedFollow
            {
                Step = step,
                Context = e,
                Expiry = e.Time + Math.Max(0.1f, step.Seconds),
                Met = new bool[Math.Max(1, step.Conditions.Count)],
            };

            // Offered the event that armed it first: their own steps routinely watch
            // for the same line that started the whole thing.
            if (!TryAdvance(armed, e)) _armed.Add(armed);
        }
    }

    private void FireStep(FollowUp s, TriggerEvent context, IReadOnlyDictionary<string, string>? caps = null)
    {
        var seconds = s.UseEventDuration && context.Value > 0.1f ? context.Value : s.Duration;
        var icon = s.ShowIcon ? (context.IconId != 0 ? context.IconId : s.IconId) : 0;

        var text = s.TextEnabled && !string.IsNullOrWhiteSpace(s.Text) ? Substitute(s.Text, context, caps) : "";
        var speech = s.TtsEnabled
            ? Substitute(string.IsNullOrWhiteSpace(s.TtsText) ? s.Text : s.TtsText, context, caps)
            : "";

        if (string.IsNullOrWhiteSpace(text) && string.IsNullOrWhiteSpace(speech)) return;

        Fired++;
        Say?.Invoke(new UserCall(
            s.Id, text, speech, seconds, s.UseEventDuration && s.ShowCountdown, icon));
    }

    // Delays that ran out, steps that were on a timer, and anything that has waited
    // long enough to be given up on.
    public void Tick(double now)
    {
        TickFightTime(now);

        for (var i = _waiting.Count - 1; i >= 0; i--)
        {
            if (_waiting[i].Due > now) continue;
            var (_, trigger, e) = _waiting[i];
            _waiting.RemoveAt(i);
            Fire(trigger, e);
        }

        for (var i = _waitingSteps.Count - 1; i >= 0; i--)
        {
            if (_waitingSteps[i].Due > now) continue;
            var (_, step, context) = _waitingSteps[i];
            _waitingSteps.RemoveAt(i);
            FireStep(step, context);
        }

        for (var i = _armed.Count - 1; i >= 0; i--)
            if (_armed[i].Expiry <= now) _armed.RemoveAt(i);

        for (var i = _clearWatch.Count - 1; i >= 0; i--)
            if (_clearWatch[i].Expiry <= now) _clearWatch.RemoveAt(i);
    }

    // ---- follow-ups ----------------------------------------------------------

    private sealed class ArmedFollow
    {
        public required FollowUp Step { get; init; }
        public required TriggerEvent Context { get; init; }
        public required double Expiry { get; init; }
        public required bool[] Met { get; set; }
        public TriggerEvent? Fired { get; set; }
        public IReadOnlyDictionary<string, string>? Caps { get; set; }
    }

    private void AdvanceArmed(TriggerEvent e)
    {
        for (var i = _armed.Count - 1; i >= 0; i--)
        {
            if (e.Time > _armed[i].Expiry) _armed.RemoveAt(i);
            else if (TryAdvance(_armed[i], e)) _armed.RemoveAt(i);
        }
    }

    // A step waits for its conditions to come in, in any order, and remembers which
    // have. The first one that lands is the one whose words the step speaks.
    private bool TryAdvance(ArmedFollow a, TriggerEvent e)
    {
        if (!KindMatches(a.Step.On, e)) return false;

        var conditions = a.Step.Conditions;
        var moved = false;

        for (var i = 0; i < conditions.Count; i++)
        {
            if (a.Met[i] || !ConditionMatches(a.Step.On, conditions[i], e)) continue;

            a.Met[i] = true;
            moved = true;
            if (a.Fired is null)
            {
                a.Fired = e;
                a.Caps = CaptureCondition(conditions[i], e);
            }
        }

        if (conditions.Count == 0 && !moved)
        {
            a.Fired = e;
            a.Met = [true];
            moved = true;
        }

        if (!moved) return false;
        if (a.Step.RequireAll && Array.Exists(a.Met, met => !met)) return false;

        FireStep(a.Step, a.Fired ?? e, a.Caps);
        return true;
    }

    private static bool KindMatches(FollowUpOn on, TriggerEvent e) => on switch
    {
        FollowUpOn.Cast => e.Kind is TriggerEventKind.CastStart or TriggerEventKind.Ability,
        FollowUpOn.CastStart => e.Kind == TriggerEventKind.CastStart,
        FollowUpOn.CastEnd => e.Kind == TriggerEventKind.CastFinish,
        FollowUpOn.Ability => e.Kind == TriggerEventKind.Ability,
        FollowUpOn.StatusGain => e.Kind == TriggerEventKind.StatusGain,
        FollowUpOn.StatusLose => e.Kind == TriggerEventKind.StatusLose,
        FollowUpOn.Headmarker => e.Kind == TriggerEventKind.Headmarker,
        FollowUpOn.Tether => e.Kind == TriggerEventKind.Tether,
        FollowUpOn.Death => e.Kind == TriggerEventKind.Death,
        FollowUpOn.Chat => e.Kind == TriggerEventKind.Chat,
        _ => false,
    };

    // Their condition matcher, kind by kind. The order and the special cases are
    // theirs: a chat line has nobody to filter on, a marker or a tether is matched on
    // its id alone, and a status counts either as one arriving or as one somebody is
    // already carrying.
    private bool ConditionMatches(FollowUpOn on, FollowCondition c, TriggerEvent e)
    {
        if (!KindMatches(on, e)) return false;

        if (on == FollowUpOn.Chat)
        {
            if (string.IsNullOrWhiteSpace(c.Pattern)) return true;
            return c.UseRegex
                ? RegexMatch(c.Pattern, e.Name)
                : e.Name.Contains(c.Pattern, StringComparison.OrdinalIgnoreCase);
        }

        if (c.Source != SourceFilter.Anyone && e.SourceSide != SideOf(c.Source)) return false;
        if (!RoleMatches(c.SourceRole, e.SourceId)) return false;
        if (!RoleMatches(c.TargetRole, e.TargetId)) return false;

        // Which end counts as you depends on the kind: a tether is either end, a
        // death is the one who died, everything else is who it landed on.
        if (c.OnlyOnSelf && _world.You != 0)
        {
            var you = _world.You;
            var aimed = on switch
            {
                FollowUpOn.Tether => e.SourceId == you || e.TargetId == you,
                FollowUpOn.Death => e.SourceId == you,
                _ => e.TargetId == you,
            };
            if (!aimed) return false;
        }

        // A marker or a tether carries an id and nothing worth matching on.
        if (on is FollowUpOn.Headmarker or FollowUpOn.Tether)
            return c.DataId == 0 || e.DataId == c.DataId;

        if (on == FollowUpOn.StatusGain)
        {
            if (StatusEventMatches(c, e)) return true;

            // Already on them, which is the case a step armed a second too late
            // would otherwise miss forever.
            var wearer = c.OnlyOnSelf ? _world.You : e.TargetId;
            return _world.HasStatus(wearer, c.MatchById ? c.DataId : 0, c.Pattern);
        }

        if (c.MatchById && c.DataId != 0) return e.DataId == c.DataId;

        if (!string.IsNullOrWhiteSpace(c.Pattern))
            return c.UseRegex
                ? RegexMatch(c.Pattern, e.Name)
                : e.Name.Contains(c.Pattern, StringComparison.OrdinalIgnoreCase);

        return true;
    }

    private static bool StatusEventMatches(FollowCondition c, TriggerEvent e)
    {
        if (c.MatchById && c.DataId != 0 && e.DataId == c.DataId) return true;
        if (!string.IsNullOrWhiteSpace(c.Pattern))
            return e.Name.Contains(c.Pattern, StringComparison.OrdinalIgnoreCase);
        return !c.MatchById || c.DataId == 0;
    }

    // ---- clearing ------------------------------------------------------------

    private void WatchForClears(TriggerEvent e)
    {
        for (var i = _clearWatch.Count - 1; i >= 0; i--)
        {
            var (expiry, trigger) = _clearWatch[i];
            if (e.Time > expiry) { _clearWatch.RemoveAt(i); continue; }
            if (!ClearMatches(trigger.ClearOn, e)) continue;

            _clearWatch.RemoveAt(i);
            Say?.Invoke(new UserCall(trigger.Id, "", "", 0f, false, 0) { ClearsOwner = true });
        }
    }

    private bool ClearMatches(ClearRule r, TriggerEvent e)
    {
        if (!KindMatches(r.On, e)) return false;
        if (r.OnlyOnSelf && !AimedAtYou(e)) return false;
        if (r.MatchById) return e.DataId == r.DataId;
        if (string.IsNullOrEmpty(r.Pattern)) return true;
        return e.Name.Contains(r.Pattern, StringComparison.OrdinalIgnoreCase);
    }

    // ---- matching ------------------------------------------------------------

    private bool Matches(UserTrigger t, TriggerEvent e)
    {
        if (!t.AnyZone && t.Zones.Count > 0 && !t.Zones.Contains(_world.Territory)) return false;
        if (!SelfRoleMatches(t.SelfRoles)) return false;
        if (!OnKindMatches(t.On, e)) return false;

        // A chat trigger reads the line and nothing else: there is no caster, no
        // target and no id on one.
        if (t.On == TriggerMatch.Chat)
        {
            if (string.IsNullOrEmpty(t.Pattern)) return true;
            return t.UseRegex
                ? RegexMatch(t.Pattern, e.Name)
                : e.Name.Contains(t.Pattern, StringComparison.OrdinalIgnoreCase);
        }

        if (t.Source != SourceFilter.Anyone && e.SourceSide != SideOf(t.Source)) return false;
        if (t.OnlyOnSelf && !AimedAtYou(e)) return false;

        if (!RoleMatches(t.SourceRole, e.SourceId)) return false;
        if (!RoleMatches(t.TargetRole, e.TargetId)) return false;
        if (!NameContains(t.SourceName, e.SourceName)) return false;
        if (!NameContains(t.TargetName, e.TargetName)) return false;
        if (!NumMatches(t, e)) return false;
        if (!VarMatches(t, e)) return false;

        if (t.MatchById) return e.DataId == t.DataId;
        if (string.IsNullOrEmpty(t.Pattern)) return true;

        return t.UseRegex
            ? RegexMatch(t.Pattern, e.Name)
            : e.Name.Contains(t.Pattern, StringComparison.OrdinalIgnoreCase);
    }

    private static bool OnKindMatches(TriggerMatch on, TriggerEvent e) => on switch
    {
        TriggerMatch.Any => true,
        TriggerMatch.Cast => e.Kind is TriggerEventKind.CastStart or TriggerEventKind.Ability,
        TriggerMatch.CastStart => e.Kind == TriggerEventKind.CastStart,
        TriggerMatch.CastEnd => e.Kind == TriggerEventKind.CastFinish,
        TriggerMatch.Ability => e.Kind == TriggerEventKind.Ability,
        TriggerMatch.StatusGain => e.Kind == TriggerEventKind.StatusGain,
        TriggerMatch.StatusLose => e.Kind == TriggerEventKind.StatusLose,
        TriggerMatch.Death => e.Kind == TriggerEventKind.Death,
        TriggerMatch.Headmarker => e.Kind == TriggerEventKind.Headmarker,
        TriggerMatch.Tether => e.Kind == TriggerEventKind.Tether,
        TriggerMatch.Chat => e.Kind == TriggerEventKind.Chat,
        _ => false,
    };

    // A tether counts either way round: it is about you whether you are the end
    // holding it or the end being held.
    private bool AimedAtYou(TriggerEvent e)
    {
        var you = _world.You;
        if (you == 0) return true;

        if (e.Kind == TriggerEventKind.Tether) return e.SourceId == you || e.TargetId == you;
        if (e.IsStatus || e.Kind == TriggerEventKind.Headmarker) return e.TargetId == you;
        return true;
    }

    private static ActorSide SideOf(SourceFilter filter) => filter switch
    {
        SourceFilter.Enemy => ActorSide.Enemy,
        SourceFilter.You => ActorSide.You,
        SourceFilter.Party => ActorSide.Party,
        _ => ActorSide.Other,
    };

    private bool RoleMatches(RoleFilter want, uint actorId) =>
        want == RoleFilter.Any || _world.RoleOf(actorId) == want;

    private bool SelfRoleMatches(RoleMask roles)
    {
        if (roles == RoleMask.None) return true;

        return _world.YourRole switch
        {
            RoleFilter.Tank => roles.HasFlag(RoleMask.Tank),
            RoleFilter.Healer => roles.HasFlag(RoleMask.Healer),
            RoleFilter.Dps => roles.HasFlag(RoleMask.Dps),
            _ => true,
        };
    }

    private static bool NameContains(string want, string actual) =>
        string.IsNullOrWhiteSpace(want)
        || actual.Contains(want, StringComparison.OrdinalIgnoreCase);

    private bool NumMatches(UserTrigger t, TriggerEvent e)
    {
        foreach (var condition in t.NumConditions)
        {
            var value = ReadField(condition.Field, e);

            // Health nothing knows is not health of zero, and a trigger gated on it
            // must not fire because the answer was missing.
            if (condition.Field is NumField.SourceHpPct or NumField.TargetHpPct && value < 0f)
                return false;

            if (!Compare(value, condition.Op, condition.Value)) return false;
        }
        return true;
    }

    private float ReadField(NumField f, TriggerEvent e) => f switch
    {
        NumField.StackCount => e.Count,
        NumField.Value => e.Value,
        NumField.Param1 => e.Param1,
        NumField.Param2 => e.Param2,
        NumField.Param3 => e.Param3,
        NumField.Param4 => e.Param4,
        NumField.SourceHpPct => _world.HealthPercent(e.SourceId),
        NumField.TargetHpPct => _world.HealthPercent(e.TargetId),
        _ => 0f,
    };

    private static bool Compare(float a, NumOp op, float b) => op switch
    {
        NumOp.Eq => Math.Abs(a - b) < 0.0001f,
        NumOp.Ne => Math.Abs(a - b) >= 0.0001f,
        NumOp.Lt => a < b,
        NumOp.Le => a <= b,
        NumOp.Gt => a > b,
        NumOp.Ge => a >= b,
        _ => false,
    };

    // ---- variables -----------------------------------------------------------

    private bool VarMatches(UserTrigger t, TriggerEvent e)
    {
        foreach (var condition in t.VarConditions)
        {
            if (string.IsNullOrWhiteSpace(condition.Name)) continue;

            var have = _vars.GetValueOrDefault(condition.Name) ?? "";
            var want = Substitute(condition.Value, e, null);

            var ok = condition.Numeric
                ? Compare(AsNumber(have), condition.Op, AsNumber(want))
                : CompareText(string.Compare(have, want, StringComparison.OrdinalIgnoreCase), condition.Op);

            if (!ok) return false;
        }
        return true;
    }

    private static bool CompareText(int order, NumOp op) => op switch
    {
        NumOp.Eq => order == 0,
        NumOp.Ne => order != 0,
        NumOp.Lt => order < 0,
        NumOp.Le => order <= 0,
        NumOp.Gt => order > 0,
        NumOp.Ge => order >= 0,
        _ => false,
    };

    private void ApplyVars(UserTrigger t, TriggerEvent e, IReadOnlyDictionary<string, string>? caps)
    {
        foreach (var action in t.SetVars)
        {
            if (string.IsNullOrWhiteSpace(action.Name)) continue;

            var value = Substitute(action.Value, e, caps);
            if (action.Op == VarOp.Increment)
            {
                var by = float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var step)
                    ? step : 1f;
                _vars[action.Name] =
                    (AsNumber(_vars.GetValueOrDefault(action.Name)) + by).ToString(CultureInfo.InvariantCulture);
            }
            else _vars[action.Name] = value;
        }
    }

    private static float AsNumber(string? text) =>
        float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;

    // ---- words ---------------------------------------------------------------

    // Their tokens, all of them: ${group} from the pattern's own captures, {$var}
    // from whatever a trigger has set, and the fixed ones for the event itself.
    public string Substitute(string text, TriggerEvent e, IReadOnlyDictionary<string, string>? caps)
    {
        if (string.IsNullOrEmpty(text)) return text;

        if (caps is not null && text.Contains("${", StringComparison.Ordinal))
            text = CaptureToken.Replace(text,
                m => caps.TryGetValue(m.Groups[1].Value, out var value) ? value : m.Value);

        if (text.Contains("{$", StringComparison.Ordinal))
            text = VarToken.Replace(text,
                m => _vars.GetValueOrDefault(m.Groups[1].Value) ?? "");

        text = text
            .Replace("{name}", e.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{cast}", e.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{status}", e.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{ability}", e.Name, StringComparison.OrdinalIgnoreCase)
            .Replace("{source}", e.SourceName, StringComparison.OrdinalIgnoreCase)
            .Replace("{target}", e.TargetName, StringComparison.OrdinalIgnoreCase);

        if (text.Contains("{player}", StringComparison.OrdinalIgnoreCase))
            text = text.Replace("{player}", FirstName(e.SourceName), StringComparison.OrdinalIgnoreCase);

        if (text.Contains("{job}", StringComparison.OrdinalIgnoreCase))
            text = text.Replace("{job}", _world.JobOf(e.SourceId), StringComparison.OrdinalIgnoreCase);

        return text;
    }

    // A voice saying a full name says a surname nobody uses.
    private static string FirstName(string full)
    {
        if (string.IsNullOrEmpty(full)) return full;
        var space = full.IndexOf(' ');
        return space > 0 ? full[..space] : full;
    }

    private Dictionary<string, string>? Capture(UserTrigger t, TriggerEvent e)
    {
        if (!t.UseRegex || string.IsNullOrEmpty(t.Pattern)) return null;
        return NamedGroups(t.Pattern, e.Name);
    }

    private Dictionary<string, string>? CaptureCondition(FollowCondition c, TriggerEvent e)
    {
        if (!c.UseRegex || string.IsNullOrEmpty(c.Pattern)) return null;
        return NamedGroups(c.Pattern, e.Name);
    }

    private Dictionary<string, string>? NamedGroups(string pattern, string input)
    {
        var regex = Compiled(pattern);
        if (regex is null) return null;

        var match = regex.Match(input);
        if (!match.Success) return null;

        var caps = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var name in regex.GetGroupNames())
        {
            if (int.TryParse(name, out _)) continue;
            var group = match.Groups[name];
            if (group.Success) caps[name] = group.Value;
        }
        return caps.Count > 0 ? caps : null;
    }

    private bool RegexMatch(string pattern, string input) => Compiled(pattern)?.IsMatch(input) ?? false;

    // A pattern somebody typed can be nonsense, and a nonsense pattern is a trigger
    // that never matches rather than a plugin that throws on every event.
    private Regex? Compiled(string pattern)
    {
        if (_patterns.TryGetValue(pattern, out var cached)) return cached;

        try
        {
            var regex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            _patterns[pattern] = regex;
            return regex;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // Their own rule: a trigger told not to re-enter cannot also stack.
    private static Concurrency ModeOf(UserTrigger t) =>
        t.NoReentry && t.Concurrency == Concurrency.Stack ? Concurrency.Wait : t.Concurrency;
}
