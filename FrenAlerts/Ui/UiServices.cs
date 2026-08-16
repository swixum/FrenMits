using Dalamud.IoC;
using Dalamud.Plugin.Services;

namespace FrenAlerts.Ui;

public class UiServices
{
    [PluginService] public static ITextureProvider Textures { get; private set; } = null!;
    [PluginService] public static IDataManager Data { get; private set; } = null!;
    [PluginService] public static ICondition Condition { get; private set; } = null!;

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
}
