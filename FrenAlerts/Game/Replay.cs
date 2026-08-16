using FFXIVClientStructs.FFXIV.Client.Game;
using Dalamud.Game.ClientState.Conditions;
using FFXIVClientStructs.FFXIV.Client.System.Framework;

namespace FrenAlerts.Game;

// Whether what is being watched is a recording, and where it is up to.
//
// All of it off the maintained struct, so nothing here is a guess about a layout.
public static unsafe class Replay
{
    // True while a recording is playing, paused included: paused is still a replay,
    // and treating it as live would let the clock run on while nothing happens.
    //
    // Off the duty condition rather than the replay manager's own controls. The
    // other plugin in this repo has read it this way through several patches and it
    // works with the recorder; the manager's controls were what this used to read,
    // and in a real Dancing Mad replay the calls came out frozen.
    public static bool InPlayback
    {
        get
        {
            try
            {
                return Service.Condition[ConditionFlag.DutyRecorderPlayback];
            }
            catch
            {
                return false;
            }
        }
    }

    public static bool Paused
    {
        get
        {
            try
            {
                var m = ContentsReplayManager.Instance();
                return m is not null && m->PlaybackControls == ContentsReplayPlaybackControl.Paused;
            }
            catch
            {
                return false;
            }
        }
    }

    // Seconds into the recording. Zero when nothing is playing.
    public static double Position
    {
        get
        {
            try
            {
                var m = ContentsReplayManager.Instance();
                return m is null ? 0 : m->PositionMs / 1000.0;
            }
            catch
            {
                return 0;
            }
        }
    }

    // How fast the simulation is running: 1 normal, 0 paused, 2 at double.
    //
    // The framework's own multiplier rather than the replay manager's, same as the
    // other plugin. This is what makes fast forward work: a fight watched at 4x has
    // to age four seconds per second or every gap between mechanics collapses.
    public static float Speed
    {
        get
        {
            try
            {
                var fw = Framework.Instance();
                if (fw is null) return 1f;
                var s = fw->GameSpeedMultiplier;
                // Garbage guard, then snap a near-zero to a hard stop so a paused
                // replay ages nothing at all rather than crawling.
                if (s is < 0f or > 100f) return 1f;
                return s < 0.02f ? 0f : s;
            }
            catch
            {
                return 1f;
            }
        }
    }
}
