using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace FrenAlerts.Ui;

public class UiServices
{
    [PluginService] public static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] public static IDataManager Data { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;
    [PluginService] public static IGameGui GameGui { get; private set; } = null!;

    private static bool _created;

    // Injected on first use, so nothing has to be added to the plugin's startup.
    public static void Ensure()
    {
        if (_created) return;
        _created = true;
        try { Service.PluginInterface.Create<UiServices>(); }
        catch (System.Exception ex) { Service.Log.Error(ex, "could not reach the texture services"); }
    }

    public static bool Ready => _created && Textures != null && Data != null;

    public static bool InCombat
    {
        get
        {
            Ensure();
            try { return Condition != null && Condition[Dalamud.Game.ClientState.Conditions.ConditionFlag.InCombat]; }
            catch { return false; }
        }
    }

    // Whether the player has hidden the game's own interface, which is the key
    // people press to take a screenshot.
    //
    // False on any doubt, the same as the flag above: refusing to draw the calls
    // because a service could not be reached would take the whole overlay off
    // mid-pull, which is far worse than a call appearing in a picture.
    public static bool GameUiHidden
    {
        get
        {
            Ensure();
            try { return GameGui is { GameUiHidden: true }; }
            catch { return false; }
        }
    }
}
