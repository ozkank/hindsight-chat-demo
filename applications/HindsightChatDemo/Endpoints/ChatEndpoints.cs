using HindsightChatDemo.Configuration;
using HindsightChatDemo.Models;
using HindsightChatDemo.Services;
using Microsoft.Extensions.Options;

namespace HindsightChatDemo.Endpoints;

/// <summary>The MCP-driven chat surface: the agent decides, via the LLM, when to call retain/recall/reflect.</summary>
public static class ChatEndpoints
{
    public static void MapChatEndpoints(this WebApplication app)
    {
        app.MapPost("/api/chat", HandleChatAsync);
        app.MapGet("/api/config", HandleConfig);
    }

    private static async Task<IResult> HandleChatAsync(ChatRequest request, HindsightAgentService agentService)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new { error = "message is required" });
        }

        var sessionId = string.IsNullOrWhiteSpace(request.SessionId)
            ? Guid.NewGuid().ToString("n")
            : request.SessionId;

        try
        {
            var (message, toolCalls) = await agentService.SendMessageAsync(sessionId, request.Message);
            return Results.Ok(new ChatResponse
            {
                Message = message,
                ToolCalls = [.. toolCalls],
                SessionId = sessionId,
            });
        }
        catch (InvalidOperationException ex)
        {
            return Results.Json(new { error = ex.Message }, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    private static IResult HandleConfig(IOptions<HindsightOptions> hindsightOptions)
    {
        var options = hindsightOptions.Value;
        return Results.Ok(new
        {
            bankId = options.BankId,
            adminUiUrl = options.AdminUiUrl,
        });
    }
}
