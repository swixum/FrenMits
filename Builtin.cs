using System.Collections.Generic;

namespace FrenMits;

// Registry of fights that ship with baked mit timelines + resync anchors.
public static class Builtin
{
    public const ushort DmuTerritory = 1363;
    public const ushort FruTerritory = 1238;

    public static readonly (ushort Territory, string Name)[] Fights =
    {
        (DmuTerritory, "Dancing Mad (Ultimate)"),
        (FruTerritory, "Futures Rewritten (Ultimate)"),
    };

    public static bool Has(uint territory) => territory is DmuTerritory or FruTerritory;

    public static string Name(uint territory) => territory switch
    {
        FruTerritory => "Futures Rewritten (Ultimate)",
        _ => "Dancing Mad (Ultimate)"
    };

    public static string[] Slots(uint territory) => territory == FruTerritory ? FruData.Slots : DmuData.Slots;

    public static List<MitLine> BuildLines(uint territory, string slot) =>
        territory == FruTerritory ? FruData.BuildLines(slot) : DmuData.BuildLines(slot);

    public static List<SyncPoint> SyncPoints(uint territory) =>
        territory == FruTerritory ? FruData.SyncPoints() : DmuData.SyncPoints();

    public static List<BossAnchor> BossAnchors(uint territory) =>
        territory == FruTerritory ? FruData.BossAnchors() : DmuData.BossAnchors();
}
