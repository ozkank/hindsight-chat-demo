using System.Collections.Concurrent;
using HindsightChatDemo.Configuration;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;
using OllamaSharp;
using HindsightChatDemo.Models;

namespace HindsightChatDemo.Services;

/// <summary>
/// Owns the Hindsight MCP connection and the Ollama-backed AIAgent, and keeps one
/// AgentSession per chat sessionId in memory so a "Yeni oturum" click can start clean
/// while an existing session keeps its history for the life of the process.
/// </summary>
public sealed class HindsightAgentService : IAsyncDisposable
{
    private readonly OllamaOptions _ollamaOptions;
    private readonly HindsightOptions _hindsightOptions;
    private readonly ToolCallRecorder _recorder;
    private readonly ILogger<HindsightAgentService> _logger;
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    private AIAgent? _agent;
    private McpClient? _mcpClient;
    private ChatClientAgentRunOptions? _runOptions;
    private Exception? _initError;
    private AIFunction? _retainTool;

    // Carries the raw user message into the FunctionInvoker closure below, scoped per
    // request via AsyncLocal (same pattern as ToolCallRecorder) since this service is a
    // singleton and requests can run concurrently.
    private readonly AsyncLocal<string?> _currentMessage = new();

    public HindsightAgentService(
        IOptions<OllamaOptions> ollamaOptions,
        IOptions<HindsightOptions> hindsightOptions,
        ToolCallRecorder recorder,
        ILogger<HindsightAgentService> logger)
    {
        _ollamaOptions = ollamaOptions.Value;
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

            _retainTool = tools.OfType<AIFunction>().FirstOrDefault(t => t.Name == "retain");
            if (_retainTool is null)
            {
                _logger.LogWarning(
                    "Could not find the retain tool among the loaded MCP tools; the " +
                    "misrouted-recall safety net (see DeclarativeStatementDetector) will be disabled.");
            }

            IChatClient chatClient = new OllamaApiClient(new Uri(_ollamaOptions.BaseUrl), _ollamaOptions.Model);

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
            _runOptions = new ChatClientAgentRunOptions(new ChatOptions
            {
                Tools = tools,
                Temperature = _ollamaOptions.Temperature,
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

                    if (context.Function.Name == "recall")
                    {
                        await GuardAgainstMisroutedNewFactAsync(cancellationToken);
                    }

                    _logger.LogInformation("Invoking tool {ToolName}", context.Function.Name);
                    var result = await context.Function.InvokeAsync(context.Arguments, cancellationToken);
                    var arguments = context.Arguments.ToDictionary(kv => kv.Key, kv => kv.Value);
                    _recorder.Record(context.Function.Name, arguments);
                    _logger.LogInformation("Recorded tool call {ToolName}", context.Function.Name);
                    return result;
                };
            }
            else
            {
                _logger.LogWarning("Could not find FunctionInvokingChatClient in the agent pipeline; tool calls won't be captured for the UI.");
            }

            _agent = agent;

            _logger.LogInformation(
                "Hindsight agent initialized with {ToolCount} tools from {Endpoint} (temperature={Temperature})",
                tools.Count, endpoint, _ollamaOptions.Temperature);
        }
        catch (Exception ex)
        {
            _initError = ex;
            _logger.LogError(ex, "Failed to initialize Hindsight agent. Is Docker/Hindsight/Ollama running?");
        }
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

        _currentMessage.Value = message;
        _recorder.BeginCapture();
        var response = await _agent.RunAsync(message, session, _runOptions, cancellationToken);
        var toolCalls = _recorder.EndCapture();
        _currentMessage.Value = null;

        return (response.Text, toolCalls);
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

    // Found by testing: llama3.1:8b sometimes calls recall for a message that is actually a
    // brand-new fact, not a reference to something already said -- e.g. "geçen hafta
    // taşındım" ("I moved last week") gets routed to recall instead of retain, purely because
    // it shares the phrase "geçen hafta" with the recall example in system_message.txt. When
    // that happens the new fact is silently never saved. Same class of issue as
    // GreetingDetector: a real model limitation on surface-pattern matching, not something a
    // prompt rewrite reliably fixes (see CLAUDE.md). We can't stop the model from calling
    // recall once it's decided to, so instead this makes sure the fact still gets saved: if
    // the message doesn't look like an actual question/reference (see
    // DeclarativeStatementDetector), retain is called too, alongside recall. A redundant
    // retain is harmless -- Hindsight's own consolidation step dedupes it against anything
    // already stored -- but a silently dropped fact is not.
    private async Task GuardAgainstMisroutedNewFactAsync(CancellationToken cancellationToken)
    {
        var message = _currentMessage.Value;
        if (string.IsNullOrWhiteSpace(message) || _retainTool is null)
        {
            return;
        }

        if (DeclarativeStatementDetector.LooksLikeQuestion(message))
        {
            return;
        }

        _logger.LogWarning(
            "recall was called for a message with no question markers ('{Message}') -- likely " +
            "a misrouted new fact. Calling retain as a safety net so it isn't lost.", message);

        var safetyNetArgs = new AIFunctionArguments
        {
            ["content"] = message,
            ["context"] = "otomatik yedek kayıt (recall yanlış tetiklendi)",
        };

        await _retainTool.InvokeAsync(safetyNetArgs, cancellationToken);
        _recorder.Record("retain", safetyNetArgs.ToDictionary(kv => kv.Key, kv => kv.Value));
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
