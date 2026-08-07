namespace HindsightChatDemo.Models;

public sealed class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
    public string? SessionId { get; set; }
}

public sealed class ChatResponse
{
    public string Message { get; set; } = string.Empty;
    public List<ToolCallInfo> ToolCalls { get; set; } = [];
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string SessionId { get; set; } = string.Empty;
}

public sealed record ToolCallInfo(string Name, IReadOnlyDictionary<string, object?> Arguments);

public sealed class HealthResponse
{
    public bool Healthy { get; set; }
    public HealthDependency Api { get; set; } = new();
    public HealthDependency Hindsight { get; set; } = new();
}

public sealed class HealthDependency
{
    public bool Up { get; set; }
    public string? Detail { get; set; }
}
