using System;
using System.Linq;

namespace FrenMits;

// FRENMITS share codes: a FightProfile serialized to a clipboard-friendly
// string. FRENMITS2 is gzip-compressed; FRENMITS1 (plain base64) still imports.
public static class PlanCodes
{
    public static string Encode(FightProfile fight)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(fight);
        // FRENMITS2 = gzip-compressed, so a full raid plan is a much shorter,
        // paste-friendly code to share.
        var raw = System.Text.Encoding.UTF8.GetBytes(json);
        using var ms = new System.IO.MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal))
            gz.Write(raw, 0, raw.Length);
        return "FRENMITS2:" + Convert.ToBase64String(ms.ToArray());
    }

    // True when the text even looks like a plan code (either generation).
    public static bool LooksLikeCode(string? text)
    {
        var t = (text ?? "").Trim();
        return t.StartsWith("FRENMITS2:") || t.StartsWith("FRENMITS1:");
    }

    // The pure half of Import: a plan code back into the fight it carries, or null
    // when the text isn't a code or won't decode. Split out from Import so a code
    // can be round-tripped in a test without a plugin or a config.
    public static FightProfile? Decode(string? codeText)
    {
        try
        {
            var text = (codeText ?? "").Trim();
            string json;
            if (text.StartsWith("FRENMITS2:"))
            {
                var data = Convert.FromBase64String(text["FRENMITS2:".Length..]);
                using var ms = new System.IO.MemoryStream(data);
                using var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionMode.Decompress);
                using var outMs = new System.IO.MemoryStream();
                gz.CopyTo(outMs);
                json = System.Text.Encoding.UTF8.GetString(outMs.ToArray());
            }
            else if (text.StartsWith("FRENMITS1:"))
            {
                json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(text["FRENMITS1:".Length..]));
            }
            else return null;

            return Newtonsoft.Json.JsonConvert.DeserializeObject<FightProfile>(json);
        }
        catch { return null; }
    }

    // Decode a FRENMITS plan code and apply it: a same-territory code UPDATES the
    // existing profile in place (the sender's active slot only, notes merged);
    // anything else is added as a new fight.
    public static (FightProfile? Fight, bool IsNew, string Message) Import(Plugin plugin, string? clipboardText)
    {
        var config = plugin.Config;
        try
        {
            if (!LooksLikeCode(clipboardText))
                return (null, false, "No FrenMits plan code on the clipboard.");

            var fight = Decode(clipboardText);
            if (fight == null) return (null, false, "That plan code couldn't be read.");
            // Codes from older versions carry MT/OT/D1-style names; standardize
            // before matching so slots line up with the receiver's (normalized) data.
            SlotNames.NormalizeFight(fight);

            // A same-territory import UPDATES the existing profile instead of
            // adding a duplicate: a second profile for one territory never fires
            // (ActiveFight takes the first match), and a duplicate of a built-in
            // renders locked, with no way to delete it.
            var existing = fight.TerritoryId != 0
                ? config.Fights.FirstOrDefault(f => f.TerritoryId == fight.TerritoryId)
                : null;
            if (existing != null)
            {
                plugin.Snapshots.Save(existing, $"before importing \"{fight.Name}\"");
                // Slot-scoped update: the import replaces the sender's ACTIVE slot
                // only, since wholesale-replacing SavedSlots/DeletedCalls would wipe
                // your saved edits for every other slot in the fight.
                existing.Lines = fight.Lines;
                existing.TimerOffset = fight.TimerOffset;
                // Sheet notes MERGE: take the sender's note where they wrote one,
                // keep yours everywhere else (wholesale replace would wipe your
                // notes with a v131-era code's empty list).
                foreach (var n in fight.Notes)
                {
                    existing.Notes.RemoveAll(o =>
                        string.Equals(o.Mechanic.Trim(), n.Mechanic.Trim(), StringComparison.OrdinalIgnoreCase)
                        && MathF.Abs(o.Time - n.Time) < 4f);
                    existing.Notes.Add(n);
                }
                if (!string.IsNullOrEmpty(fight.Slot))
                {
                    existing.Slot = fight.Slot;
                    existing.SavedSlots[fight.Slot] = fight.Lines;
                    existing.DeletedCalls.RemoveAll(d =>
                        string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase));
                    existing.DeletedCalls.AddRange(fight.DeletedCalls.Where(d =>
                        string.Equals(d.Slot, fight.Slot, StringComparison.OrdinalIgnoreCase)));
                }
                if (!Builtin.Has(existing.TerritoryId))
                {
                    // Custom fights carry their hand-built anchors + sheet layout;
                    // built-ins keep the canonical baked ones (ApplySlot refreshes
                    // those anyway).
                    existing.Name = fight.Name;
                    existing.SyncPoints = fight.SyncPoints;
                    existing.BossAnchors = fight.BossAnchors;
                    if (fight.CustomSlots is { Count: > 0 })
                    {
                        existing.CustomSlots = fight.CustomSlots;
                        existing.CustomRows = fight.CustomRows ?? new();
                        existing.CustomDowntimes = fight.CustomDowntimes ?? new();
                    }
                }
                config.Save();
                return (existing, false, string.IsNullOrEmpty(fight.Slot)
                    ? $"Imported \"{fight.Name}\" into your existing \"{existing.Name}\"."
                    : $"Imported \"{fight.Name}\" into your existing \"{existing.Name}\" ({fight.Slot} slot; your other slots kept).");
            }

            fight.Id = Guid.NewGuid().ToString("N");
            config.Fights.Add(fight);
            config.Save();
            return (fight, true, $"Imported \"{fight.Name}\".");
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "FrenMits: import failed");
            return (null, false, "That plan code couldn't be read.");
        }
    }
}
