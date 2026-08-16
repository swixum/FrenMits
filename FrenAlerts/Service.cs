using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace FrenAlerts;

public class Service
{
    [PluginService] public static IDalamudPluginInterface PluginInterface { get; private set; } = null!;
    [PluginService] public static ICommandManager CommandManager { get; private set; } = null!;
    [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
    [PluginService] public static IPluginLog Log { get; private set; } = null!;

    [PluginService] public static IFramework Framework { get; private set; } = null!;
    [PluginService] public static IClientState ClientState { get; private set; } = null!;
    [PluginService] public static IObjectTable ObjectTable { get; private set; } = null!;
    [PluginService] public static IPartyList PartyList { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;

    // Control packets carry the direction calls, and the object table cannot see them.
    [PluginService] public static IGameInteropProvider GameInterop { get; private set; } = null!;

    // Boss lines are matched by the row id of the line, so the client's own yell
    // sheet is what turns an incoming line back into an id. See Game/YellEvents.cs.
    [PluginService] public static IDataManager DataManager { get; private set; } = null!;
}
