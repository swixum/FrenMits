using System.Collections.Generic;

namespace FrenMits.Callouts.Fights;

// Dancing Mad, phase 4: Kefka Says.
//
// The whole phase turns on one question that no debuff id answers. Every debuff
// is handed out twice over, once meaning what it says and once meaning the
// opposite, and which it is this time is written on the boss: a status the two
// adds carry, whose stack count is the answer. 1122 and 1120 mean the debuffs
// are real; 1121 and 1119 mean they lie.
//
// So the phase is read in two beats. The boss says real or fake, then the
// debuffs land, and the call is the pair. That is why this is code rather than
// rows in the pack: a row watching a debuff id can only ever say half of it,
// and the half it says is wrong every other time.
//
// The wording is copied exactly from where these calls came from. Not shortened
// and not made friendlier: people already know these words.
//
// Whether the ice and the thunder lie is written somewhere else again: on the
// markers over Kefka's head, ten of them across the phase, whose meaning is
// their position in it.
//
// What is still missing is the second half of the phase, where the debuffs from
// both rounds are in the air at once and a call has to name which of the two it
// is talking about. That needs the resolve order, and the resolve order needs a
// recorded pull rather than a reading of the source.
public static class DancingMadKefkaSays
{
    public const uint Territory = 1363;

    // The status the two adds wear, whose stack count says whether this round's
    // debuffs mean what they say.
    private const uint Tell = 2056;

    private const uint NeoExdeathReal = 1122;
    private const uint ChaosReal = 1120;

    // The two adds that wear it.
    private const uint NeoExdeath = 19510;
    private const uint Chaos = 19507;

    // The debuffs handed out in the first two rounds.
    private const uint Shriek = 5543;
    private const uint Lightning = 5544;
    private const uint Water = 5545;
    private const uint Acceleration = 5546;

    // The third round: a wound and something that decides whether you want it.
    private const uint WhiteWound = 5541;
    private const uint WhiteWoundFake = 4887;
    private const uint BlackWound = 5542;
    private const uint BlackWoundFake = 4888;
    private const uint AllaganField = 454;
    private const uint BeyondDeath = 1382;
    private const uint BeyondDeathFake = 5464;

    // Chaos's own pair, on their own clock.
    private const uint Entropy = 5547;
    private const uint DynamicFluid = 5548;

    // What the boss last said. Read back by every call in the round.
    private const string NeNote = "dmu-ne-real";
    private const string ChNote = "dmu-ch-real";

    // The first round's accelerations run past this, the second round's do not,
    // which is what tells a player which of the two they are looking at. These
    // are the reference's own thresholds, kept exactly.
    private const float FirstSetSplit = 60f;
    private const float SecondSetSplit = 45f;

    // A round takes tens of seconds, so this is long enough that one round
    // speaks once and short enough that the next round still speaks.
    private const float PerRound = 20f;

    public static FightModule Module() => new()
    {
        Name = "Dancing Mad P4",
        Territory = Territory,
        Triggers = Build(),
    };

    private static bool Real(TriggerContext c, string note) => c.State.Noted(note) == "1";

    // Whether the boss has said anything yet this round. Before it has, no call
    // here can be right, so none of them speak.
    private static bool Answered(TriggerContext c, string note) => c.State.HasNote(note);

    private static List<Trigger> Build()
    {
        var t = new List<Trigger>
        {
            // Beat one: the boss says which way this round runs. Silent, because
            // it is not a call, it is the thing every call reads.
            new()
            {
                Key = "DMU P4 Tell",
                On = new TriggerMatch { Kind = EventKind.StatusGain, Id = Tell },
                // No words and no voice, so it is already silent, and no About,
                // so the page never offers it as a switch. Switching it off
                // would take every call in the phase quiet with it, which is
                // not a choice worth offering.
                Note = c =>
                {
                    if (c.Event.Target.NameId == NeoExdeath)
                        c.State.Note(NeNote, c.Event.Extra == NeoExdeathReal ? "1" : "0");
                    else if (c.Event.Target.NameId == Chaos)
                        c.State.Note(ChNote, c.Event.Extra == ChaosReal ? "1" : "0");
                },
            },
        };

        // Beat two: whichever of the four you were handed, said back with
        // whether it means it. One trigger, not one per round: two triggers
        // both matched the first round and both spoke, at two different
        // thresholds, so the first round said "long" and "short" at once.
        t.Add(Round());

        // Chaos hands out its own pair on its own clock, and its own status
        // says whether they mean it.
        t.Add(DynEnt());

        // Round three: a wound plus the thing that decides whether you want to
        // be hit by it.
        t.Add(Wounds());

        // And what to do when the first round's debuffs come due. A fake water
        // resolves the way a real lightning does, so what you do about yours is
        // the debuff and the tell together, not the debuff.
        t.Add(BombResolves());
        t.Add(DonutOrCircle());
        t.Add(DonutOrCircleMove());

        // Whether the ice and the thunder are lying, which is written over
        // Kefka's head rather than in a status. The counter goes first: every
        // marker call reads its own position in the phase.
        t.Add(MarkerCounter());
        t.Add(MysteryMagic());

        // The three casts that lie the same way the debuffs do, and read the
        // same status to prove it.
        t.Add(Lying("Grand Cross", 47892, NeNote, "Cross"));
        t.Add(Lying("Inferno", 47904, ChNote, "Inferno"));
        t.Add(Lying("Tsunami", 47905, ChNote, "Tsunami"));

        return t;
    }

    private const string AccelKey = "DMU P4 Kefka Says Set";

    // The acceleration/water/lightning hand. Fires off whichever of them lands,
    // and reads the rest of the hand out of the status book, so the order the
    // four arrive in cannot change what it says.
    private static Trigger Round() => new()
    {
        Key = AccelKey,
        About = "Real or Fake, Short or Long Accel, Water, Lightning",
        On = new TriggerMatch
        {
            Kind = EventKind.StatusGain,
            Target = ActorScope.Me,
        },
        Suppress = PerRound,
        Duration = 8f,
        When = c => c.Event.Id is Acceleration or Water or Lightning
                    && Answered(c, NeNote),
        Says = c =>
        {
            var real = Real(c, NeNote);

            // How many times this has already spoken, which is which round we
            // are in. The two rounds split short from long at their own length,
            // so the same 50 seconds is long in the first and short in the
            // second, and calling it by the wrong one sends you the wrong way.
            var split = c.State.Count(AccelKey) == 0 ? FirstSetSplit : SecondSetSplit;
            var accel = c.MyStatus(Acceleration);
            var shriek = c.Have(Shriek);

            if (accel.Present)
            {
                var isShort = accel.Shorter(split);
                var text = (real, isShort, shriek) switch
                {
                    (true, true, true) => "Real Short + Shriek",
                    (true, true, false) => "Real Short Accel",
                    (true, false, true) => "Real Long + Shriek",
                    (true, false, false) => "Real Long Accel",
                    // The reference says "Fake Short Accel" for both of these,
                    // and it is copied rather than tidied.
                    (false, true, true) => "Fake Short Accel",
                    (false, true, false) => "Fake Short Accel",
                    (false, false, true) => "Fake Long + Shriek",
                    (false, false, false) => "Fake Long Accel",
                };
                return new Say(text, Severity: CallSeverity.Warn);
            }

            if (c.Have(Water))
                return new Say(real ? "Real Water" : "Fake Water", Severity: CallSeverity.Warn);
            if (c.Have(Lightning))
                return new Say(real ? "Real Lightning" : "Fake Lightning", Severity: CallSeverity.Warn);

            // Handed nothing this round, which is a real outcome and not a call.
            return null;
        },
    };

    private static Trigger DynEnt() => new()
    {
        Key = "DMU P4 Kefka Says Entropy Dynamic",
        About = "Real or Fake Entropy, Real or Fake Dynamic",
        On = new TriggerMatch { Kind = EventKind.StatusGain, Target = ActorScope.Me },
        Suppress = PerRound,
        Duration = 8f,
        When = c => c.Event.Id is Entropy or DynamicFluid && Answered(c, ChNote),
        Says = c =>
        {
            var real = Real(c, ChNote);
            var dynamic = c.Event.Id == DynamicFluid;
            return new Say(
                (real, dynamic) switch
                {
                    (true, true) => "Real Dynamic",
                    (true, false) => "Real Entropy",
                    (false, true) => "Fake Dynamic",
                    (false, false) => "Fake Entropy",
                },
                Severity: CallSeverity.Warn);
        },
    };

    // Kefka, who wears the two markers that say whether the ice and the thunder
    // are lying.
    private const uint Kefka = 18475;

    // The marker that means that half is lying. Its opposite number is simply
    // any other marker, which is what upstream tests for too: it asks whether
    // this one is present, not which one it is.
    private const uint FakeIceMarker = 675;
    private const uint FakeThunderMarker = 677;

    // Kefka wears ten of these across the phase, and they are not all the same
    // shape. The first six are three pairs, each pair one call. The seventh and
    // eighth are read one at a time, and the last two as a pair, and those four
    // together are the mana charge: they say nothing on their own, they change
    // what the last two already said.
    //
    // So the marker's position in the phase is what it means. A pair read off
    // the seventh and eighth would be a fourth call at a moment that has none.
    private const string MarkerCount = "dmu-mm-count";
    private const int PairedMarkers = 6;
    private const int AllMarkers = 10;

    private static string MarkerNote(int n) => "dmu-mm-" + n;

    private static int Seen(TriggerContext c)
        => int.TryParse(c.State.Noted(MarkerCount), out var n) ? n : 0;

    private static bool Was(TriggerContext c, int n, uint markerId)
        => c.State.Noted(MarkerNote(n)) == markerId.ToString();

    // Counts them and remembers each one, so anything downstream can ask what
    // the third was rather than only what the last one was.
    private static Trigger MarkerCounter() => new()
    {
        Key = "DMU P4 Marker Order",
        On = new TriggerMatch { Kind = EventKind.HeadMarker },
        When = c => c.Event.Target.NameId == Kefka && Seen(c) < AllMarkers,
        Note = c =>
        {
            var n = Seen(c) + 1;
            c.State.Note(MarkerCount, n.ToString());
            c.State.Note(MarkerNote(n), c.Event.Id.ToString());
        },
    };

    // The three pairs. Neither marker means anything alone: ice without thunder
    // is half an answer, and half an answer here is the wrong half of the room.
    private static Trigger MysteryMagic() => new()
    {
        Key = "DMU P4 Mystery Magic",
        About = "Avoid Both, Stand in Both, or which of the two to stand in",
        On = new TriggerMatch { Kind = EventKind.HeadMarker },
        Duration = 9f,
        When = c => c.Event.Target.NameId == Kefka
                    && Seen(c) is var n && n <= PairedMarkers && n % 2 == 0,
        Says = c =>
        {
            var n = Seen(c);
            var fakeIce = Was(c, n - 1, FakeIceMarker) || Was(c, n, FakeIceMarker);
            var fakeThunder = Was(c, n - 1, FakeThunderMarker) || Was(c, n, FakeThunderMarker);

            return new Say((fakeIce, fakeThunder) switch
            {
                (false, false) => "Avoid Both",
                (false, true) => "Out of Cones, In Lines",
                (true, false) => "In Cones, Out of Lines",
                (true, true) => "Stand in Both",
            }, Severity: CallSeverity.Danger);
        },
    };

    // Whether each half lies once the mana charge has been through it.
    //
    // The charge is a toggle, not a statement: the single marker read before it
    // against the pair read after, exclusive-or. Said the other way round, the
    // pair flips whatever the single said. The RESULT is what a call wants, so
    // these are named for the result. Calling them "flipped" got the two of
    // them backwards in the tests that were meant to check them.
    //
    // Nothing calls off these yet. They are here because the calls that will
    // need them need them to be right, and this part can be checked against a
    // recording on its own.
    public static bool ManaChargeKnown(TriggerContext c) => Seen(c) >= AllMarkers;

    public static bool ThunderLies(TriggerContext c)
        => Was(c, 7, FakeThunderMarker) ^ (Was(c, 9, FakeThunderMarker) || Was(c, 10, FakeThunderMarker));

    public static bool IceLies(TriggerContext c)
        => Was(c, 8, FakeIceMarker) ^ (Was(c, 9, FakeIceMarker) || Was(c, 10, FakeIceMarker));

    // A cast that lies the same way the debuffs do. Three of these, and each
    // reads the same status on whichever add owns it, so the word in front of
    // the mechanic's name is the boss's own answer rather than a guess.
    private static Trigger Lying(string mechanic, uint cast, string note, string word) => new()
    {
        Key = $"DMU P4 {mechanic}",
        About = $"Real or Fake {word}",
        On = new TriggerMatch { Kind = EventKind.CastStart, Id = cast, Source = ActorScope.Enemy },
        Duration = 6f,
        When = c => Answered(c, note),
        Says = c => new Say($"{(Real(c, note) ? "Real" : "Fake")} {word}",
            Severity: CallSeverity.Warn),
    };

    // The four casts the donut-or-circle resolves on.
    private static readonly uint[] EntropyCasts = { 47906, 47907, 47908, 47909 };

    // How long before a debuff comes due its call goes out. Upstream picks this
    // moment off its own place in the sequence; this reads it off the debuff's
    // own clock instead, which lands at the same point and does not need the
    // rest of the sequence to have been followed.
    private const float BeforeItLands = 20f;

    // What to do with the first round's water or lightning when it comes due.
    // Fake flips it: a fake water resolves the way a real lightning does.
    private static Trigger BombResolves() => new()
    {
        Key = "DMU P4 Kefka Says Bomb Resolves",
        About = "Stack or Spread, whichever your debuff really means",
        On = new TriggerMatch { Kind = EventKind.StatusGain, Target = ActorScope.Me },
        BeforeExpiry = BeforeItLands,
        Suppress = PerRound,
        Duration = 8f,
        When = c => c.Event.Id is Water or Lightning
                    && Answered(c, NeNote)
                    // The acceleration hand words this differently, and that
                    // wording is built from state nothing reads yet.
                    && !c.Have(Acceleration),
        Says = c =>
        {
            var fake = !Real(c, NeNote);
            var lightning = c.Event.Id == Lightning;

            // Lightning spreads and water stacks, and fake turns each into the
            // other. Written as the one exclusive-or it is rather than as four
            // branches that have to be kept agreeing.
            var stack = lightning == fake;
            return new Say(stack ? "Stack" : "Spread", Severity: CallSeverity.Warn);
        },
    };

    // Whether the thing you are carrying resolves as a donut or as a circle.
    // The debuff says one, the tell can say the other, and only both together
    // say which way to stand.
    private static bool IsDonut(TriggerContext c)
    {
        var fake = !Real(c, ChNote);
        return c.Have(DynamicFluid) != fake;
    }

    private static Trigger DonutOrCircle() => new()
    {
        Key = "DMU P4 Kefka Says Entropy Dynamic Resolves",
        About = "Stack for Donut, or Stack then Move",
        On = new TriggerMatch { Kind = EventKind.StatusGain, Target = ActorScope.Me },
        BeforeExpiry = BeforeItLands,
        Suppress = PerRound,
        Duration = 8f,
        When = c => c.Event.Id is Entropy or DynamicFluid && Answered(c, ChNote),
        Says = c => new Say(IsDonut(c) ? "Stack for Donut" : "Stack then Move",
            Severity: CallSeverity.Warn),
    };

    // And the moment it goes off: stay put for a donut, get out for a circle.
    private static Trigger DonutOrCircleMove() => new()
    {
        Key = "DMU P4 Kefka Says Entropy Dynamic Move",
        About = "Move or Stay, as it goes off",
        On = new TriggerMatch { Kind = EventKind.CastStart },
        Suppress = 6f,
        Duration = 5f,
        When = c => System.Array.IndexOf(EntropyCasts, c.Event.Id) >= 0
                    && Answered(c, ChNote)
                    && (c.Have(Entropy) || c.Have(DynamicFluid))
                    // Only the first of the two. After the mana charge the same
                    // cast wants a different sentence, built from what the last
                    // four markers changed, and this one would answer with a
                    // reading that is one round out of date.
                    && Seen(c) < PairedMarkers + 1,
        Says = c => new Say(IsDonut(c) ? "Stay" : "Move", Severity: CallSeverity.Danger),
    };

    // Round three. The wound says which side of the room kills you and the
    // other debuff says whether you want to be killed, and the boss's own
    // status flips both readings at once.
    private static Trigger Wounds() => new()
    {
        Key = "DMU P4 Kefka Says Wounds",
        About = "Real or Fake White or Black, with Death or Allag",
        On = new TriggerMatch { Kind = EventKind.StatusGain, Target = ActorScope.Me },
        Suppress = PerRound,
        Duration = 10f,
        When = c => c.Event.Id is WhiteWound or WhiteWoundFake or BlackWound or BlackWoundFake
                        or AllaganField or BeyondDeath or BeyondDeathFake
                    && Answered(c, NeNote),
        Says = c =>
        {
            var real = Real(c, NeNote);
            var white = c.AnyOf(WhiteWound, WhiteWoundFake).Present;
            var black = c.AnyOf(BlackWound, BlackWoundFake).Present;
            var death = c.AnyOf(BeyondDeath, BeyondDeathFake).Present;
            var allagan = c.Have(AllaganField);

            // Both halves are needed. One of them alone is the burst arriving
            // out of order, and the next event in it will ask again.
            if (!white && !black) return null;
            if (!death && !allagan) return null;

            var side = white ? "White" : "Black";
            var other = death ? "Death" : "Allag";
            return new Say($"{(real ? "Real" : "Fake")} {side} + {other}",
                Severity: CallSeverity.Danger);
        },
    };
}
