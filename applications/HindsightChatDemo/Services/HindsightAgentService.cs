using System.ClientModel;
using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using HindsightChatDemo.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using OllamaSharp;
using OpenAI;
using HindsightChatDemo.Models;

namespace HindsightChatDemo.Services;

/// <summary>
/// Owns the Hindsight MCP connection and the Ollama-backed AIAgent, and keeps one
/// AgentSession per chat sessionId in memory so a "Yeni oturum" click can start clean
/// while an existing session keeps its history for the life of the process.
/// </summary>
public sealed class HindsightAgentService : IAsyncDisposable
{
    private readonly LlmOptions _llmOptions;
    private readonly OllamaOptions _ollamaOptions;
    private readonly NvidiaNimOptions _nvidiaNimOptions;
    private readonly HindsightOptions _hindsightOptions;
    private readonly ToolCallRecorder _recorder;
    private readonly ILogger<HindsightAgentService> _logger;
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    private AIAgent? _agent;
    private McpClient? _mcpClient;
    private ChatClientAgentRunOptions? _runOptions;
    private Exception? _initError;

    public HindsightAgentService(
        IOptions<LlmOptions> llmOptions,
        IOptions<OllamaOptions> ollamaOptions,
        IOptions<NvidiaNimOptions> nvidiaNimOptions,
        IOptions<HindsightOptions> hindsightOptions,
        ToolCallRecorder recorder,
        ILogger<HindsightAgentService> logger)
    {
        _llmOptions = llmOptions.Value;
        _ollamaOptions = ollamaOptions.Value;
        _nvidiaNimOptions = nvidiaNimOptions.Value;
        _hindsightOptions = hindsightOptions.Value;
        _recorder = recorder;
        _logger = logger;
    }

    public bool IsReady => _agent is not null;
    public string? InitError => _initError?.Message;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var endpoint = _hindsightOptions.ResolveMcpEndpoint();

            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(endpoint),
            });
            _mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            var allMcpTools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);

            // Hindsight exposes many MCP tools; this demo is specifically about retain,
            // recall and reflect, and keeping the tool list short also makes tool-call
            // selection far more reliable for a small local model.
            IList<AITool> tools = allMcpTools
                .Where(t => t.Name is "retain" or "recall" or "reflect")
                .Cast<AITool>()
                .ToList();

            _logger.LogInformation(
                "Available Hindsight MCP tools matching retain/recall/reflect: {Tools}",
                string.Join(", ", tools.Select(t => t.Name)));

            var (chatClient, modelLabel, temperature) = BuildChatClient();

            var systemMessage = await File.ReadAllTextAsync(
                Path.Combine(AppContext.BaseDirectory, "system_message.txt"), cancellationToken);

            var agent = new ChatClientAgent(
                chatClient,
                instructions: systemMessage,
                name: "DestekHattiAsistani",
                description: null,
                tools: tools,
                loggerFactory: null,
                services: null);

            // Lower temperature makes retain/recall tool-call selection (and the follow-up
            // reply) noticeably more consistent for small local models. NOTE: a RunOptions.ChatOptions
            // that omits Tools silently wipes the agent's configured tools for that call
            // (see https://github.com/microsoft/agent-framework/issues/1453), so Tools is
            // re-specified here even though it's identical to what the agent already has.
            //
            // MaxOutputTokens caps the final customer-facing reply. Found by testing
            // (2026-08-26): with no cap, a small local model can occasionally ramble for
            // hundreds of tokens instead of the short reply system_message.txt asks for
            // (seen once describing Hindsight's raw JSON result field-by-field, in English,
            // instead of answering) -- each extra generated token adds real wall-clock time
            // on local Ollama, so this both bounds worst-case latency and discourages that
            // failure mode.
            //
            // 1024, not 200: a *reasoning* NvidiaNim model (e.g. nemotron-3-super-120b-a12b)
            // spends part of this budget on an invisible "thinking" pass (returned separately
            // as reasoning_content, not shown to the customer) before it can even decide on a
            // tool call or write the real reply -- 200 was cut off mid-thought, producing an
            // empty response and no tool call at all (confirmed 2026-08-26: recall silently
            // returned "" with zero tool calls). 1024 was enough headroom in testing for both
            // the plain Ollama model and this reasoning model; raise further only if a real
            // reply is ever seen truncated mid-sentence.
            _runOptions = new ChatClientAgentRunOptions(new ChatOptions
            {
                Tools = tools,
                Temperature = temperature,
                MaxOutputTokens = 1024,
            });

            // ChatClientAgent wraps our IChatClient in its own pipeline (approval handling,
            // function invocation, ...). Reach through it via GetService to find the
            // FunctionInvokingChatClient that actually calls retain/recall, so every
            // tool call can be captured for the transparency layer in the UI.
            if (agent.ChatClient.GetService(typeof(FunctionInvokingChatClient)) is FunctionInvokingChatClient functionInvokingClient)
            {
                functionInvokingClient.FunctionInvoker = async (context, cancellationToken) =>
                {
                    if (context.Function.Name is "recall" or "reflect")
                    {
                        EnforceMinimumMaxTokens(context.Arguments, context.Function.Name);
                    }

                    _logger.LogInformation("Invoking tool {ToolName}", context.Function.Name);
                    var result = await context.Function.InvokeAsync(context.Arguments, cancellationToken);
                    var arguments = context.Arguments.ToDictionary(kv => kv.Key, kv => kv.Value);
                    _recorder.Record(context.Function.Name, arguments);
                    _logger.LogInformation("Recorded tool call {ToolName}", context.Function.Name);

                    if (context.Function.Name == "reflect")
                    {
                        CaptureReflectRawText(result);
                    }

                    return result;
                };
            }
            else
            {
                _logger.LogWarning("Could not find FunctionInvokingChatClient in the agent pipeline; tool calls won't be captured for the UI.");
            }

            _agent = agent;

            _logger.LogInformation(
                "Hindsight agent initialized with {ToolCount} tools from {Endpoint}, provider={Provider}, model={Model} (temperature={Temperature})",
                tools.Count, endpoint, _llmOptions.Provider, modelLabel, temperature);
        }
        catch (Exception ex)
        {
            _initError = ex;
            _logger.LogError(ex, "Failed to initialize Hindsight agent. Is Docker/Hindsight running, and (if using {Provider}) is the LLM backend reachable?", _llmOptions.Provider);
        }
    }

    /// <summary>
    /// Builds the IChatClient for whichever provider Llm:Provider selects. Ollama talks its
    /// native /api/chat via OllamaSharp (see CLAUDE.md for why -- not the OpenAI-compat layer).
    /// NvidiaNim is a genuinely OpenAI-compatible endpoint, so the OpenAI SDK talks to it
    /// directly with just the base URL swapped -- this is the exact swap CLAUDE.md already
    /// flagged as the escape hatch if OllamaApiClient ever needed replacing.
    /// </summary>
    private (IChatClient ChatClient, string ModelLabel, float Temperature) BuildChatClient() => _llmOptions.Provider switch
    {
        LlmProvider.NvidiaNim => BuildNvidiaNimChatClient(),
        LlmProvider.Ollama => (
            new OllamaApiClient(new Uri(_ollamaOptions.BaseUrl), _ollamaOptions.Model),
            _ollamaOptions.Model,
            _ollamaOptions.Temperature),
        _ => throw new NotSupportedException($"Unknown Llm:Provider '{_llmOptions.Provider}'."),
    };

    private (IChatClient ChatClient, string ModelLabel, float Temperature) BuildNvidiaNimChatClient()
    {
        if (string.IsNullOrWhiteSpace(_nvidiaNimOptions.ApiKey))
        {
            throw new InvalidOperationException(
                "Llm:Provider is NvidiaNim but NvidiaNim:ApiKey is not set. Get a free key at " +
                "https://build.nvidia.com and set it via the NvidiaNim__ApiKey environment variable.");
        }

        var openAiClient = new OpenAIClient(
            new ApiKeyCredential(_nvidiaNimOptions.ApiKey),
            new OpenAIClientOptions { Endpoint = new Uri(_nvidiaNimOptions.BaseUrl) });

        IChatClient chatClient = openAiClient.GetChatClient(_nvidiaNimOptions.Model).AsIChatClient();
        return (chatClient, _nvidiaNimOptions.Model, _nvidiaNimOptions.Temperature);
    }

    public async Task<(string Message, IReadOnlyList<ToolCallInfo> ToolCalls)> SendMessageAsync(
        string sessionId, string message, CancellationToken cancellationToken = default)
    {
        if (_agent is null)
        {
            throw new InvalidOperationException($"Hindsight agent not ready: {_initError?.Message ?? "unknown error"}");
        }

        // See GreetingDetector's doc comment: this model reliably mis-fires retain with
        // garbled content on bare greetings, and prompt-only fixes didn't help. Short-circuit
        // before the message ever reaches the model.
        if (GreetingDetector.IsGreetingOnly(message))
        {
            return (GreetingDetector.PickReply(sessionId), []);
        }

        var session = await GetOrCreateSessionAsync(sessionId, cancellationToken);

        _recorder.BeginCapture();
        var response = await _agent.RunAsync(message, session, _runOptions, cancellationToken);
        var toolCalls = _recorder.EndCapture();
        var reflectRawText = _recorder.TakeLastReflectRawText();

        var replyText = ApplyReflectMetaAnswerFallback(response.Text, reflectRawText);

        return (replyText, toolCalls);
    }

    // Found by testing (2026-08-13/14): after a system-prompt fix stopped recall/reflect
    // replies from leaking English, a second, separate model quirk stayed on reflect
    // specifically -- the final reply describes that an answer exists ("Bu cevap ...
    // sunar.") instead of stating it, discarding a real, grounded answer that reflect's
    // own MCP result already contains (captured via CaptureReflectRawText below). A second
    // round of prompt hardening aimed squarely at this (see system_message.txt) reduced but
    // did not eliminate it -- same "prompt alone isn't enough" pattern as GreetingDetector,
    // so it's fixed here in code instead: detect the self-describing opener and, when
    // reflect actually ran this turn, substitute its real answer.
    private static readonly Regex MetaAnswerOpener = new(
        @"^\s*Bu\s+(cevap|değerlendirme|bilgi|yanıt|sonuç|özet)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private string ApplyReflectMetaAnswerFallback(string replyText, string? reflectRawText)
    {
        if (string.IsNullOrWhiteSpace(reflectRawText) || !MetaAnswerOpener.IsMatch(replyText))
        {
            return replyText;
        }

        _logger.LogWarning(
            "Reply described its own answer instead of giving it ({Reply}) -- substituting reflect's raw result.",
            replyText);

        // The UI renders this as plain text (see wwwroot/app.js), not Markdown, so strip the
        // heading/emphasis syntax reflect's answer tends to include rather than show it raw.
        var plain = Regex.Replace(reflectRawText, @"^#{1,6}\s*", "", RegexOptions.Multiline);
        plain = Regex.Replace(plain, @"\*\*(.+?)\*\*", "$1");
        plain = Regex.Replace(plain, @"(?<!\w)_(.+?)_(?!\w)", "$1");
        return plain.Trim();
    }

    private void CaptureReflectRawText(object? toolResult)
    {
        // reflect's MCP result shape: { "structuredContent": { "text": "<answer>", ... }, ... }
        // (confirmed by logging the raw JsonElement during development -- not documented on
        // Hindsight's API reference page, same as the /memories/list endpoint noted in
        // CLAUDE.md).
        try
        {
            if (toolResult is JsonElement root
                && root.TryGetProperty("structuredContent", out var structured)
                && structured.TryGetProperty("text", out var textProp)
                && textProp.ValueKind == JsonValueKind.String)
            {
                var text = textProp.GetString();
                if (!string.IsNullOrWhiteSpace(text))
                {
                    _recorder.RecordReflectRawText(text);
                }
            }
            // No else: a missing structuredContent.text is expected when reflect's own MCP
            // call errored (e.g. a malformed argument the model sent -- see CLAUDE.md's
            // NvidiaNim battery notes) or found nothing. The meta-answer fallback below
            // simply no-ops without a captured text in that case; the agent's own reply
            // (an honest "couldn't find anything" or similar) is used as-is.
        }
        catch (Exception ex)
        {
            // Best-effort safety net only -- if Hindsight ever changes this result shape,
            // fall back to whatever the agent said rather than breaking the chat turn.
            _logger.LogWarning(ex, "Could not extract reflect's raw answer text from the tool result.");
        }
    }

    // Found by testing: the model sometimes picks a tiny max_tokens for recall/reflect
    // (seen as low as 10) -- Hindsight then has no room to fit even one fact and returns
    // zero results, which the agent (correctly, but unhelpfully) reports as "I don't know."
    // Verified directly against Hindsight's REST API: the exact same query returned 0
    // results at max_tokens=10 and 1 correct result at max_tokens=1024. This clamps the
    // argument up before the call goes out, the same pattern as GreetingDetector -- a
    // proven model quirk fixed in code rather than by asking the prompt to behave.
    private const int MinRecallMaxTokens = 500;

    private void EnforceMinimumMaxTokens(AIFunctionArguments arguments, string toolName)
    {
        if (!arguments.TryGetValue("max_tokens", out var raw) || raw is null)
        {
            return;
        }

        if (!int.TryParse(raw.ToString(), out var requested) || requested >= MinRecallMaxTokens)
        {
            return;
        }

        _logger.LogWarning(
            "{Tool} requested max_tokens={Requested}, too small to return any real content -- raising it to {Min}.",
            toolName, requested, MinRecallMaxTokens);
        arguments["max_tokens"] = MinRecallMaxTokens;
    }

    private async Task<AgentSession> GetOrCreateSessionAsync(string sessionId, CancellationToken cancellationToken)
    {
        if (_sessions.TryGetValue(sessionId, out var existing))
        {
            return existing;
        }

        var session = await _agent!.CreateSessionAsync(cancellationToken);
        _sessions[sessionId] = session;
        return session;
    }

    public async ValueTask DisposeAsync()
    {
        if (_mcpClient is not null)
        {
            await _mcpClient.DisposeAsync();
        }
    }
}
