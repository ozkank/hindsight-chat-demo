using System.Collections.Concurrent;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
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
    private readonly IConfiguration _config;
    private readonly ToolCallRecorder _recorder;
    private readonly ILogger<HindsightAgentService> _logger;
    private readonly ConcurrentDictionary<string, AgentSession> _sessions = new();

    private AIAgent? _agent;
    private McpClient? _mcpClient;
    private ChatClientAgentRunOptions? _runOptions;
    private Exception? _initError;

    public HindsightAgentService(IConfiguration config, ToolCallRecorder recorder, ILogger<HindsightAgentService> logger)
    {
        _config = config;
        _recorder = recorder;
        _logger = logger;
    }

    public bool IsReady => _agent is not null;
    public string? InitError => _initError?.Message;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var bankId = _config["Hindsight:BankId"] ?? "destek-hatti-demo";
            var endpointTemplate = _config["Hindsight:McpEndpoint"] ?? "http://localhost:8888/mcp/{bankId}/";
            var endpoint = endpointTemplate.Replace("{bankId}", bankId);

            var transport = new HttpClientTransport(new HttpClientTransportOptions
            {
                Endpoint = new Uri(endpoint),
            });
            _mcpClient = await McpClient.CreateAsync(transport, cancellationToken: cancellationToken);
            var allMcpTools = await _mcpClient.ListToolsAsync(cancellationToken: cancellationToken);

            // Hindsight exposes many MCP tools; this demo is specifically about retain/recall,
            // and keeping the tool list short also makes tool-call selection far more reliable
            // for a small local model like llama3.2.
            IList<AITool> tools = allMcpTools
                .Where(t => t.Name is "retain" or "recall")
                .Cast<AITool>()
                .ToList();

            var ollamaBaseUrl = _config["Ollama:BaseUrl"] ?? "http://localhost:11434";
            var model = _config["Ollama:Model"] ?? "llama3.2:latest";

            IChatClient chatClient = new OllamaApiClient(new Uri(ollamaBaseUrl), model);

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
            var temperature = _config.GetValue<float?>("Ollama:Temperature") ?? 0.3f;
            _runOptions = new ChatClientAgentRunOptions(new ChatOptions
            {
                Tools = tools,
                Temperature = temperature,
            });

            // ChatClientAgent wraps our IChatClient in its own pipeline (approval handling,
            // function invocation, ...). Reach through it via GetService to find the
            // FunctionInvokingChatClient that actually calls retain/recall, so every
            // tool call can be captured for the transparency layer in the UI.
            if (agent.ChatClient.GetService(typeof(FunctionInvokingChatClient)) is FunctionInvokingChatClient functionInvokingClient)
            {
                functionInvokingClient.FunctionInvoker = async (context, cancellationToken) =>
                {
                    var result = await context.Function.InvokeAsync(context.Arguments, cancellationToken);
                    var arguments = context.Arguments.ToDictionary(kv => kv.Key, kv => kv.Value);
                    _recorder.Record(context.Function.Name, arguments);
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
                tools.Count, endpoint, temperature);
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
