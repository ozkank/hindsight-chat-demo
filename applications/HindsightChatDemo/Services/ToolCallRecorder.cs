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

    // See HindsightAgentService.SendMessageAsync's meta-answer fallback: reflect's own MCP
    // result already contains a real, grounded Turkish answer, so it's captured here as a
    // safety net for when the agent's final reply describes that answer instead of stating
    // it (e.g. "Bu cevap ... sunar."). This is a List<string>, not an AsyncLocal<string?>,
    // on purpose -- found by testing: the framework invokes tool calls on a branched
    // execution context (see the "detached Task" comment on Record() below), so a plain
    // AsyncLocal.Value *assignment* made inside RecordReflectRawText never became visible
    // back on this method's caller's flow, even though the identical-looking Record() below
    // appeared to work. It only "worked" because List.Add mutates a shared object by
    // reference; a value re-assignment does not survive the branch the same way. Reusing
    // that same list-mutation trick here instead of debugging the framework's context flow.
    private readonly AsyncLocal<List<string>?> _reflectRawTexts = new();

    public void BeginCapture()
    {
        _current.Value = [];
        _reflectRawTexts.Value = [];
    }

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

    public void RecordReflectRawText(string text) => _reflectRawTexts.Value?.Add(text);

    public IReadOnlyList<ToolCallInfo> EndCapture()
    {
        var calls = _current.Value ?? [];
        _current.Value = null;
        return calls;
    }

    public string? TakeLastReflectRawText()
    {
        var texts = _reflectRawTexts.Value;
        _reflectRawTexts.Value = null;
        return texts is { Count: > 0 } ? texts[^1] : null;
    }
}
