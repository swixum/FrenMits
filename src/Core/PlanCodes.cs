using System;
using System.Linq;

namespace FrenMits;

// Share codes: a fight serialized to a pasteable string.
public static class PlanCodes
{
    public static string Encode(FightProfile fight)
    {
        var json = Newtonsoft.Json.JsonConvert.SerializeObject(fight);
        // FRENMITS2 is gzipped, so a raid plan pastes much shorter.
        var raw = System.Text.Encoding.UTF8.GetBytes(json);
        using var ms = new System.IO.MemoryStream();
        using (var gz = new System.IO.Compression.GZipStream(ms, System.IO.Compression.CompressionLevel.Optimal))
            gz.Write(raw, 0, raw.Length);
        return "FRENMITS2:" + Convert.ToBase64String(ms.ToArray());
    }

    // True when the text looks like a code of either generation.
    public static bool LooksLikeCode(string? text)
    {
        var t = (text ?? "").Trim();
        return t.StartsWith("FRENMITS2:") || t.StartsWith("FRENMITS1:");
    }

    // A plan code back into its fight, or null when it won't decode.
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

    // Decode and apply; a same-duty code updates in place.
    public static (FightProfile? Fight, bool IsNew, string Message) Import(Plugin plugin, string? clipboardText)
    {
        var config = plugin.Config;
        try
        {
            if (!LooksLikeCode(clipboardText))
                return (null, false, "No FrenMits plan code on the clipboard.");

            var fight = Decode(clipboardText);
            if (fight == null) return (null, false, "That plan code couldn't be read.");
            // Old codes carry legacy slot names, so standardize first.
            SlotNames.NormalizeFight(fight);

            // A same-duty import updates instead of duplicating.
            var existing = fight.TerritoryId != 0
                ? config.Fights.FirstOrDefault(f => f.TerritoryId == fight.TerritoryId)
                : null;
            if (existing != null)
            {
                plugin.Snapshots.Save(existing, $"before importing \"{fight.Name}\"");
                // Slot-scoped: only the sender's active slot is replaced.
                existing.Lines = fight.Lines;
                existing.TimerOffset = fight.TimerOffset;
                // Notes merge, so an old code can't wipe yours.
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
                    // Custom fights carry their own anchors and layout.
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
