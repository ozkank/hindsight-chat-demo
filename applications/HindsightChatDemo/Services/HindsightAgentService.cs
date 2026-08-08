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

        _recorder.BeginCapture();
        var response = await _agent.RunAsync(message, session, _runOptions, cancellationToken);
        var toolCalls = _recorder.EndCapture();

        return (response.Text, toolCalls);
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
