using System.Diagnostics;
using FrenAlerts.Engine.Alerts;

namespace FrenAlerts.Game;

// The neural voice, driven from here but running somewhere else.
//
// The model underneath is native code, and native code fails by taking its process
// down. In its own process that costs a pipe and a restart rather than the raid.
public sealed class NeuralVoice : IDisposable
{
    // Long enough to load 84MB and a model from a cold disk, short enough that a pack
    // which is never going to work is written off rather than waited on forever.
    private const double StartSeconds = 90.0;

    // Something that cannot stay up three times is broken rather than unlucky.
    private const int MaxStarts = 3;

    // The pack is six files on disk, and asking the filesystem about them on every
    // call would be disk work per line spoken.
    private const double RecheckSeconds = 10.0;

    private readonly string _folder;
    private readonly VoiceModel _pack;
    private readonly Stopwatch _clock = Stopwatch.StartNew();

    private Process? _child;
    private int _starts;
    private bool _givenUp;
    private double _startedAt;
    private double _packCheckedAt = double.NegativeInfinity;
    private bool _packReady;

    // Written on the watch thread and read on the voice thread.
    private volatile bool _ready;

    public NeuralVoice(string folder)
    {
        _folder = folder;
        _pack = new VoiceModel(name =>
        {
            var file = new FileInfo(Path.Combine(folder, name));
            return file.Exists ? file.Length : 0;
        });
    }

    public VoiceModel Pack => _pack;

    public bool Ready => PackReady() && !_givenUp;

    public IEnumerable<VoiceModel.Piece> Missing => _pack.Missing;

    public long BytesToFetch => _pack.BytesToFetch;

    public bool Speaking => !_givenUp && _ready && _child is { HasExited: false };

    public bool GivenUp => _givenUp;

    public int Spoken { get; private set; }

    // True while a child has been launched and has not said it is ready yet.
    private bool Starting => _child is { HasExited: false } && !_ready;

    // Started when speech is first wanted, not at load, so a night where nobody turns
    // it on never pays for it.
    public bool Start()
    {
        if (_givenUp || Speaking) return Speaking;

        if (Starting)
        {
            // Loading takes seconds, and every call arriving in that window used to
            // launch another 84MB process on top of the one already loading.
            if (_clock.Elapsed.TotalSeconds - _startedAt < StartSeconds) return false;

            Service.Log.Warning("Fren Alerts: the local voice did not finish loading; stopping it.");
            Stop();
        }

        if (!PackReady()) return false;

        if (_starts >= MaxStarts)
        {
            _givenUp = true;
            Service.Log.Warning("Fren Alerts: the local voice would not stay running; using the system voice.");
            return false;
        }

        Launch();
        return false;
    }

    private void Launch()
    {
        try
        {
            _starts++;
            _ready = false;
            _startedAt = _clock.Elapsed.TotalSeconds;

            _child = Process.Start(new ProcessStartInfo(Path.Combine(_folder, "FrenAlertsVoice.exe"))
            {
                WorkingDirectory = _folder,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            });

            if (_child is null) return;

            // Read on a thread, because loading takes seconds and this is reached
            // from the voice worker.
            var child = _child;
            new Thread(() => Watch(child)) { IsBackground = true, Name = "Fren Alerts voice watch" }.Start();
        }
        catch (Exception ex)
        {
            Service.Log.Warning(ex, "Fren Alerts: the local voice could not be started.");
            _child = null;
        }
    }

    private void Watch(Process child)
    {
        try
        {
            if (child.StandardOutput.ReadLine() == "ready")
            {
                _ready = true;
                Service.Log.Information("Fren Alerts: local voice ready.");
                // Blocks until it ends, which is the point: the moment it dies,
                // speech stops claiming to work.
                child.WaitForExit();
            }
            else
            {
                Service.Log.Warning("Fren Alerts: the local voice did not report itself ready.");
            }
        }
        catch
        {
            // The pipe closing is how a dead child reports itself.
        }
        finally
        {
            _ready = false;
        }
    }

    // Never blocks and never throws, because a closed pipe means the child died
    // between the check and the write, which is a lost call rather than a problem.
    public bool Say(string text)
    {
        if (!Speaking || string.IsNullOrWhiteSpace(text)) return false;

        try
        {
            // Stamped here so the child can drop what the fight has overtaken.
            var at = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
            _child!.StandardInput.WriteLine($"{at:F3}\t{text.Replace('\t', ' ')}");
            _child.StandardInput.Flush();
            Spoken++;
            return true;
        }
        catch
        {
            _ready = false;
            return false;
        }
    }

    public string Describe()
    {
        if (_givenUp) return "Local voice failed to stay running; the system voice is in use.";
        if (Speaking) return $"Local voice is running, {Spoken} said.";
        if (Starting) return "Local voice is starting.";
        if (!PackReady()) return _pack.Describe();
        return "Local voice is installed and will start when a call fires.";
    }

    // Cached, because a call that is not installed would otherwise stat six files
    // every time somebody speaks. Re-checked on an interval so installing it mid
    // session is noticed without a reload.
    private bool PackReady()
    {
        // Once it is there it stays there; a pack that vanishes mid-session shows up
        // as the child dying, which is already handled.
        if (_packReady) return true;

        var now = _clock.Elapsed.TotalSeconds;
        if (now - _packCheckedAt < RecheckSeconds) return false;

        _packCheckedAt = now;
        _packReady = _pack.Ready;
        return _packReady;
    }

    private void Stop()
    {
        _ready = false;
        try
        {
            if (_child is { HasExited: false })
            {
                // Closing input asks it to stop, which is cleaner than killing it
                // mid-sentence; the kill is the backstop.
                _child.StandardInput.Close();
                if (!_child.WaitForExit(2000)) _child.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Shutting down, and a child that will not die is the system's now.
        }
        _child?.Dispose();
        _child = null;
    }

    public void Dispose() => Stop();
}
