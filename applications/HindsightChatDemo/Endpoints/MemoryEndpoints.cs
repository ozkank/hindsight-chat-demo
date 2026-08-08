using HindsightChatDemo.Configuration;
using HindsightChatDemo.HindsightClient;
using Microsoft.Extensions.Options;

namespace HindsightChatDemo.Endpoints;

/// <summary>
/// The REST-driven counterpart to <see cref="ChatEndpoints"/>: reads memories straight
/// from Hindsight over HTTP via <see cref="IHindsightRestClient"/>, no MCP and no LLM
/// involved. Exists to demo the two integration styles side by side -- the agent WRITES
/// through MCP tool calls, this endpoint READS through a plain REST client.
/// </summary>
public static class MemoryEndpoints
{
    public static void MapMemoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/memories", HandleListMemoriesAsync);
    }

    private static async Task<IResult> HandleListMemoriesAsync(
        IHindsightRestClient client, IOptions<HindsightOptions> hindsightOptions, int limit = 20)
    {
        var bankId = hindsightOptions.Value.BankId;
        var result = await client.ListMemoriesAsync(bankId, limit);
        return Results.Ok(result);
    }
}
