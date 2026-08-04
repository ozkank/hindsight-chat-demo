using HindsightChatDemo.Models;

namespace HindsightChatDemo.Services;

/// <summary>
/// Captures retain/recall/reflect (and any other) tool calls made while handling the current
/// logical request. Backed by AsyncLocal so concurrent chat requests don't see each other's
/// tool calls, even though the underlying AIAgent instance is shared as a singleton.
/// </summary>
public sealed class ToolCallRecorder(ILogger<ToolCallRecorder> logger)
{
    private readonly AsyncLocal<List<ToolCallInfo>?> _current = new();

    public void BeginCapture() => _current.Value = [];

    public void Record(string name, IReadOnlyDictionary<string, object?> arguments)
    {
        if (_current.Value is null)
        {
            // AsyncLocal context was lost between BeginCapture and this call (e.g. the
            // framework invoked the tool on a detached Task that didn't flow ExecutionContext).
            // Surfaced as a warning so a silently-empty toolCalls array is easy to diagnose.
            logger.LogWarning("Tool call {ToolName} recorded outside an active capture scope — it will not appear in the UI.", name);
            return;
        }

        _current.Value.Add(new ToolCallInfo(name, arguments));
    }

    public IReadOnlyList<ToolCallInfo> EndCapture()
    {
        var calls = _current.Value ?? [];
        _current.Value = null;
        return calls;
    }
}
