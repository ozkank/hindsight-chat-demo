using System.ComponentModel.DataAnnotations;

namespace HindsightChatDemo.Configuration;

/// <summary>
/// Binds the "Hindsight" section of appsettings.json. See <see cref="OllamaOptions"/>
/// for why this is a typed options class instead of raw IConfiguration reads.
/// </summary>
public sealed class HindsightOptions
{
    public const string SectionName = "Hindsight";

    /// <summary>MCP endpoint template used by HindsightAgentService; "{bankId}" is replaced with <see cref="BankId"/>.</summary>
    [Required]
    public string McpEndpoint { get; set; } = "http://localhost:8888/mcp/{bankId}/";

    /// <summary>Hindsight memory namespace. All chat sessions in this demo share one.</summary>
    [Required]
    public string BankId { get; set; } = "destek-hatti-demo";

    /// <summary>
    /// Base address for Hindsight's REST API (health, memories, ...), used by
    /// <see cref="HindsightChatDemo.HindsightClient.IHindsightRestClient"/>. Same host as
    /// <see cref="McpEndpoint"/> but reached directly over HTTP instead of through MCP --
    /// this is the "raw REST client" path shown alongside the MCP tool-calling agent.
    /// </summary>
    [Required]
    public string RestBaseUrl { get; set; } = "http://localhost:8888";

    /// <summary>Shown in the UI as a link to Hindsight's own Admin UI.</summary>
    [Required]
    public string AdminUiUrl { get; set; } = "http://localhost:9999";

    /// <summary>Resolves <see cref="McpEndpoint"/>'s "{bankId}" placeholder against <see cref="BankId"/>.</summary>
    public string ResolveMcpEndpoint() => McpEndpoint.Replace("{bankId}", BankId);
}
