using FFXIVClientStructs.FFXIV.Client.Game;

namespace FrenAlerts.Game;

// Whether what is being watched is a recording, and where it is up to.
//
// All of it off the maintained struct, so nothing here is a guess about a layout.
public static unsafe class Replay
{
    // True while a recording is playing, paused included: paused is still a replay,
    // and treating it as live would let the clock run on while nothing happens.
    public static bool InPlayback
    {
        get
        {
            try
            {
                var m = ContentsReplayManager.Instance();
                return m is not null && m->PlaybackControls != ContentsReplayPlaybackControl.None;
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

    public static float Speed
    {
        get
        {
            try
            {
                var m = ContentsReplayManager.Instance();
                return m is null ? 1f : m->PlaybackSpeed;
            }
            catch
            {
                return 1f;
            }
        }
    }
}
