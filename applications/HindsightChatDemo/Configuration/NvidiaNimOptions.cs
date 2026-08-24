using System.ComponentModel.DataAnnotations;

namespace HindsightChatDemo.Configuration;

/// <summary>
/// Binds the "NvidiaNim" section of appsettings.json. Only used when Llm:Provider is
/// NvidiaNim (see LlmOptions). ApiKey is intentionally not [Required] here -- an Ollama-only
/// setup shouldn't be forced to have one -- HindsightAgentService checks it at startup
/// instead, only when this provider is actually selected, and fails the same soft way
/// (logged, /api/health reports it) as a missing Docker/Ollama today.
/// </summary>
public sealed class NvidiaNimOptions
{
    public const string SectionName = "NvidiaNim";

    /// <summary>NVIDIA's OpenAI-compatible endpoint. https://build.nvidia.com/explore/discover</summary>
    [Required]
    public string BaseUrl { get; set; } = "https://integrate.api.nvidia.com/v1";

    /// <summary>Free-tier model with function calling support; see CLAUDE.md for how this was picked.</summary>
    [Required]
    public string Model { get; set; } = "meta/llama-3.1-8b-instruct";

    /// <summary>
    /// Set via the NvidiaNim__ApiKey environment variable (or dotnet user-secrets), not
    /// appsettings.json -- never commit this. Get a free key at https://build.nvidia.com.
    /// </summary>
    public string? ApiKey { get; set; }

    [Range(0.0, 2.0)]
    public float Temperature { get; set; } = 0.3f;
}
