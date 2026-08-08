namespace HindsightChatDemo.HindsightClient;

/// <summary>
/// Talks to Hindsight's REST API directly over HTTP -- no MCP, no LLM tool-calling in
/// the loop. This is the deliberate counterpart to <see cref="HindsightChatDemo.Services.HindsightAgentService"/>,
/// which reaches Hindsight through MCP as agent tools: the agent WRITES memories by
/// deciding, via the LLM, when to call retain/recall/reflect; this client READS them
/// directly, the same way an admin dashboard or a batch job would.
/// </summary>
public interface IHindsightRestClient
{
    /// <summary>Calls GET /health. Throws on network failure -- callers decide how to report that.</summary>
    Task<HindsightHealthStatus> GetHealthAsync(CancellationToken cancellationToken = default);

    /// <summary>Calls GET /v1/default/banks/{bankId}/memories/list, newest first.</summary>
    Task<ListMemoriesResult> ListMemoriesAsync(string bankId, int limit = 20, CancellationToken cancellationToken = default);
}
