using FFXIVClientStructs.FFXIV.Client.UI;
using FrenAlerts.Engine.Alerts;

namespace FrenAlerts.Game;

// The sound a hand-written trigger asks for.
//
// Their editor calls this a sound path, and their own build plays a file. This one
// plays the game's own effects instead, the sixteen a raider already knows from
// macros: they need no file to ship, no second audio library, and no volume of their
// own to get wrong.
//
// A trigger that names something else is left alone rather than guessed at. Silence
// is a bad answer, but a wrong noise mid-pull is worse, and the page says which of
// the two a trigger is going to get.
public static class Sounds
{
    // How it is written is the engine's to read; this half only plays it.
    public const int Most = SoundChoice.Most;

    public static bool Names(string path) => SoundChoice.Names(path);

    public static int Number(string path) => SoundChoice.Number(path);

    // Plays it, on the game's own mixer, at whatever the player has the game set to.
    public static void Play(string path)
    {
        var n = Number(path);
        if (n <= 0) return;

        try
        {
            // Their numbering starts at one and the client's at zero, which is the
            // one place these two disagree: se.1 played as se.2 all the way up.
            UIGlobals.PlayChatSoundEffect((uint)(n - 1));
        }
        catch (Exception ex)
        {
            Service.Log.Warning($"Fren Alerts: sound {n} would not play, {ex.Message}");
        }
    }
}
