using HindsightChatDemo.Models;

namespace HindsightChatDemo.Services;

/// <summary>
/// Captures retain/recall (and any other) tool calls made while handling the current
/// logical request. Backed by AsyncLocal so concurrent chat requests don't see each other's
/// tool calls, even though the underlying AIAgent instance is shared as a singleton.
/// </summary>
public sealed class ToolCallRecorder
{
    private readonly AsyncLocal<List<ToolCallInfo>?> _current = new();

    public void BeginCapture() => _current.Value = [];

    public void Record(string name, IReadOnlyDictionary<string, object?> arguments) =>
        _current.Value?.Add(new ToolCallInfo(name, arguments));

    public IReadOnlyList<ToolCallInfo> EndCapture()
    {
        var calls = _current.Value ?? [];
        _current.Value = null;
        return calls;
    }
}
