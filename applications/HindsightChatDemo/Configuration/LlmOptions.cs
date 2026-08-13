using System.ComponentModel.DataAnnotations;

namespace HindsightChatDemo.Configuration;

/// <summary>Which backend HindsightAgentService builds its IChatClient against.</summary>
public enum LlmProvider
{
    /// <summary>Local Ollama via OllamaSharp's native /api/chat client. No network dependency; see OllamaOptions.</summary>
    Ollama,

    /// <summary>NVIDIA NIM's free hosted endpoints (OpenAI-compatible). Needs internet + an API key; see NvidiaNimOptions.</summary>
    NvidiaNim,
}

/// <summary>
/// Binds the "Llm" section of appsettings.json. Just a provider switch -- the
/// provider-specific settings live in OllamaOptions / NvidiaNimOptions so each
/// backend's config only has to make sense for that backend.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    [Required]
    public LlmProvider Provider { get; set; } = LlmProvider.Ollama;
}
