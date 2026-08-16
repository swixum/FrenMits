using Dalamud.Game.Chat;
using Dalamud.Game.Text;
using FrenAlerts.Engine;
using Lumina.Excel.Sheets;

namespace FrenAlerts.Game;

// Boss lines, as the row id of the line rather than its words.
//
// A yell reaches a plugin as text in the chat log, and matching text is a trap:
// it breaks for anybody not playing in English, and it breaks again when a patch
// re-punctuates a line. So the client's own NpcYell sheet is read once, the lines
// this fight cares about are turned into text using the player's own language,
// and an incoming line is matched back to its row id.
//
// No signature and no packet hook. Both halves are maintained Dalamud services,
// which is the whole reason this route was taken: see the head marker probe for
// what guessing a signature costs.
public sealed class YellEvents : IDisposable
{
    private readonly Action<GameEvent> _emit;
    private readonly Func<double> _now;

    // The lines worth listening for, by what the client says they read.
    private readonly Dictionary<string, uint> _byText = new(StringComparer.Ordinal);

    private bool _hooked;

    public YellEvents(Action<GameEvent> emit, Func<double> now)
    {
        _emit = emit;
        _now = now;
    }

    public int Reported { get; private set; }

    // How many of the wanted lines the client could actually name. A zero here is
    // the tell that the sheet moved, and it is worth saying out loud rather than
    // going quiet in the fight.
    public int Known => _byText.Count;

    // Told which yells matter as a fight loads, so this reads one small sheet
    // slice rather than every line in the game.
    public void Watch(IReadOnlySet<uint> yellIds)
    {
        _byText.Clear();
        if (yellIds.Count == 0)
        {
            Stop();
            return;
        }

        try
        {
            var sheet = Service.DataManager.GetExcelSheet<NpcYell>();
            foreach (var id in yellIds)
            {
                if (sheet.GetRowOrDefault(id) is not { } row) continue;
                var text = Clean(row.Text.ExtractText());
                if (text.Length > 0) _byText[text] = id;
            }
        }
        catch (Exception ex)
        {
            Service.Log.Error(ex, "Fren Alerts: the yell sheet would not read.");
        }

        Service.Log.Debug($"Fren Alerts: listening for {_byText.Count} of {yellIds.Count} boss lines.");
        Start();
    }

    private void Start()
    {
        if (_hooked) return;
        Service.ChatGui.ChatMessage += OnChat;
        _hooked = true;
    }

    private void Stop()
    {
        if (!_hooked) return;
        Service.ChatGui.ChatMessage -= OnChat;
        _hooked = false;
    }

    // Read only, never handled: this listens to the chat log and must not change
    // what the player sees in it.
    private void OnChat(IHandleableChatMessage message)
    {
        // A boss line arrives as one of these two and nothing else does, so the
        // rest of the chat log is never even read.
        if (message.LogKind is not
            (XivChatType.NPCDialogueAnnouncements or XivChatType.NPCDialogue)) return;

        if (!_byText.TryGetValue(Clean(message.Message.TextValue), out var id)) return;

        Reported++;
        _emit(new GameEvent
        {
            Kind = EventKind.NpcYell,
            Time = _now(),
            Id = id,
        });
    }

    // The sheet writes lines with soft hyphens and line breaks that the chat log
    // does not, so both sides are flattened before they are compared.
    private static string Clean(string s)
    {
        Span<char> buffer = stackalloc char[s.Length];
        var n = 0;
        foreach (var c in s)
        {
            if (c is '­' or '\r' or '\n') continue;
            buffer[n++] = c is ' ' ? ' ' : c;
        }
        return new string(buffer[..n]).Trim();
    }

    public void Dispose() => Stop();
}
