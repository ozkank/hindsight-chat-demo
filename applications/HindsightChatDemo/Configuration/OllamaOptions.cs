using System.ComponentModel.DataAnnotations;

namespace HindsightChatDemo.Configuration;

/// <summary>
/// Binds the "Ollama" section of appsettings.json. Kept as a typed options class
/// instead of scattered IConfiguration["Ollama:X"] reads so a missing/malformed
/// setting fails fast at startup (see Program.cs's ValidateOnStart) rather than
/// surfacing as a confusing null-reference deep inside HindsightAgentService.
/// </summary>
public sealed class OllamaOptions
{
    public const string SectionName = "Ollama";

    /// <summary>Ollama's native API address (no "/v1" -- see README for why).</summary>
    [Required]
    public string BaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Must match HINDSIGHT_API_LLM_MODEL in docker-compose.hindsight.yml.</summary>
    [Required]
    public string Model { get; set; } = "llama3.1:8b";

    /// <summary>Lower values measurably improve tool-call reliability for small local models.</summary>
    [Range(0.0, 2.0)]
    public float Temperature { get; set; } = 0.3f;
}
