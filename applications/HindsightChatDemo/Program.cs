using HindsightChatDemo.Models;
using HindsightChatDemo.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<ToolCallRecorder>();
builder.Services.AddSingleton<HindsightAgentService>();
builder.Services.AddHttpClient();

var app = builder.Build();

app.UseDefaultFiles();
app.UseStaticFiles();

// Connect to Hindsight/Ollama once at startup. If it fails (e.g. Docker isn't running yet),
// log it and keep the web server up so /api/health can report what's wrong.
await app.Services.GetRequiredService<HindsightAgentService>().InitializeAsync();

app.MapPost("/api/chat", async (ChatRequest request, HindsightAgentService agentService) =>
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
});

app.MapGet("/api/config", (IConfiguration config) => Results.Ok(new
{
    bankId = config["Hindsight:BankId"] ?? "destek-hatti-demo",
    adminUiUrl = config["Hindsight:AdminUiUrl"] ?? "http://localhost:9999",
}));

app.MapGet("/api/health", async (HindsightAgentService agentService, IConfiguration config, IHttpClientFactory httpClientFactory) =>
{
    var apiHealthy = agentService.IsReady;
    var hindsightHealthy = false;
    string? hindsightDetail = agentService.InitError;

    try
    {
        var hindsightHealthEndpoint = config["Hindsight:HealthEndpoint"] ?? "http://localhost:8888/health";
        using var client = httpClientFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(3);
        var response = await client.GetAsync(hindsightHealthEndpoint);
        hindsightHealthy = response.IsSuccessStatusCode;
        hindsightDetail ??= $"HTTP {(int)response.StatusCode}";
    }
    catch (Exception ex)
    {
        hindsightDetail ??= ex.Message;
    }

    var result = new HealthResponse
    {
        Healthy = apiHealthy && hindsightHealthy,
        Api = new HealthDependency { Up = apiHealthy, Detail = apiHealthy ? "agent ready" : agentService.InitError },
        Hindsight = new HealthDependency { Up = hindsightHealthy, Detail = hindsightDetail },
    };

    return Results.Ok(result);
});

app.Run();
