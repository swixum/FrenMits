using System.Text.RegularExpressions;

namespace FrenAlerts.Engine.Scripts;

// Job codes read out as the job.
//
// Their own engine does this on the way to the voice and nowhere else: the screen
// shows "PLD", the voice says "Paladin", because a voice reading a three letter
// code says three letters. The table is theirs, all twenty two of them.
public static class ScriptSpeech
{
    private static readonly Dictionary<string, string> Jobs = new(StringComparer.Ordinal)
    {
        ["PLD"] = "Paladin", ["WAR"] = "Warrior", ["DRK"] = "Dark Knight", ["GNB"] = "Gunbreaker",
        ["WHM"] = "White Mage", ["SCH"] = "Scholar", ["AST"] = "Astrologian", ["SGE"] = "Sage",
        ["MNK"] = "Monk", ["DRG"] = "Dragoon", ["NIN"] = "Ninja", ["SAM"] = "Samurai",
        ["RPR"] = "Reaper", ["VPR"] = "Viper", ["BRD"] = "Bard", ["MCH"] = "Machinist",
        ["DNC"] = "Dancer", ["BLM"] = "Black Mage", ["SMN"] = "Summoner", ["RDM"] = "Red Mage",
        ["PCT"] = "Pictomancer", ["BLU"] = "Blue Mage",
    };

    private static readonly Regex JobCode = new(
        @"\b(PLD|WAR|DRK|GNB|WHM|SCH|AST|SGE|MNK|DRG|NIN|SAM|RPR|VPR|BRD|MCH|DNC|BLM|SMN|RDM|PCT|BLU)\b",
        RegexOptions.Compiled);

    public static string Spell(string line) =>
        string.IsNullOrEmpty(line) ? line : JobCode.Replace(line, m => Jobs[m.Value]);
}
