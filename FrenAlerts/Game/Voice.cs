using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace FrenAlerts.Game;

public sealed class Voice : IDisposable
{
    public const int Queued = 8;

    private const double StaleSeconds = 4.0;

    private readonly BlockingCollection<(string Text, DateTime At)> _jobs = new(Queued);
    private readonly Thread _worker;
    private readonly CancellationTokenSource _stopping = new();

    private object? _engine;
    private bool _unavailable;

    public Voice()
    {
        _worker = new Thread(Run)
        {
            IsBackground = true,
            Name = "Fren Alerts voice",
        };
        _worker.Start();
    }

    public bool Enabled { get; set; }

    public float Volume { get; set; } = 0.7f;

    public int Spoken { get; private set; }
    public int Dropped { get; private set; }

    public NeuralVoice? Local { get; set; }

    // True once speech has been tried and found not to work, so the config page can
    // say so instead of the player wondering why nothing happens.
    public bool Unavailable => _unavailable;

    public void Say(string text)
    {
        if (!Enabled || _stopping.IsCancellationRequested) return;
        if (string.IsNullOrWhiteSpace(text)) return;

        // TryAdd, never Add: Add blocks when the queue is full, and this is called
        // from the frame handler.
        if (!_jobs.TryAdd((text, DateTime.UtcNow))) Dropped++;
    }

    private void Run()
    {
        foreach (var job in _jobs.GetConsumingEnumerable())
        {
            if (_stopping.IsCancellationRequested) return;

            if ((DateTime.UtcNow - job.At).TotalSeconds > StaleSeconds)
            {
                Dropped++;
                continue;
            }

            Speak(job.Text);
        }
    }

    // Windows' own speech, reached through COM so nothing has to be shipped in the
    // zip and nothing has to be downloaded before the plugin can talk.
    private void Speak(string text)
    {
        if (Local is { GivenUp: false } local)
        {
            local.Start();
            if (local.Say(text))
            {
                Spoken++;
                return;
            }
        }

        if (_unavailable) return;
        try
        {
            if (_engine is null)
            {
                var type = Type.GetTypeFromProgID("SAPI.SpVoice");
                if (type is null)
                {
                    _unavailable = true;
                    return;
                }
                _engine = Activator.CreateInstance(type);
                if (_engine is null)
                {
                    _unavailable = true;
                    return;
                }
            }

            _engine.GetType().InvokeMember("Volume",
                System.Reflection.BindingFlags.SetProperty, null, _engine,
                [(int)Math.Clamp(Volume * 100f, 0f, 100f)]);

            _engine.GetType().InvokeMember("Speak",
                System.Reflection.BindingFlags.InvokeMethod, null, _engine, [text, 0]);

            Spoken++;
        }
        catch (COMException ex)
        {
            _unavailable = true;
            Service.Log.Warning(ex, "Fren Alerts: speech is not available on this machine.");
        }
        catch (Exception ex)
        {
            _unavailable = true;
            Service.Log.Error(ex, "Fren Alerts: speech failed and has been turned off.");
        }
    }

    public void Dispose()
    {
        _stopping.Cancel();
        _jobs.CompleteAdding();
        _worker.Join(TimeSpan.FromSeconds(2));

        if (_engine is not null && Marshal.IsComObject(_engine)) Marshal.ReleaseComObject(_engine);
        _engine = null;
        _jobs.Dispose();
        _stopping.Dispose();
    }
}
