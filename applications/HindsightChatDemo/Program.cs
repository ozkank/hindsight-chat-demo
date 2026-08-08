using HindsightChatDemo.Configuration;
using HindsightChatDemo.Endpoints;
using HindsightChatDemo.HealthChecks;
using HindsightChatDemo.HindsightClient;
using HindsightChatDemo.Services;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// --- Configuration: typed + validated at startup instead of scattered IConfiguration["..."] reads. ---
builder.Services
    .AddOptions<OllamaOptions>()
    .Bind(builder.Configuration.GetSection(OllamaOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<HindsightOptions>()
    .Bind(builder.Configuration.GetSection(HindsightOptions.SectionName))
    .ValidateDataAnnotations()
    .ValidateOnStart();

// --- Core services ---
builder.Services.AddSingleton<ToolCallRecorder>();
builder.Services.AddSingleton<HindsightAgentService>();

// Typed HttpClient for Hindsight's REST API -- the counterpart to HindsightAgentService's
// MCP connection. BaseAddress is resolved from HindsightOptions once at registration time.
builder.Services.AddHttpClient<IHindsightRestClient, HindsightRestClient>((sp, http) =>
{
    var options = sp.GetRequiredService<IOptions<HindsightOptions>>().Value;
    http.BaseAddress = new Uri(options.RestBaseUrl);
    http.Timeout = TimeSpan.FromSeconds(10);
});

// --- Health Checks: replaces the old hand-rolled /api/health HTTP call. ---
builder.Services.AddHealthChecks()
    .AddCheck<AgentHealthCheck>("agent")
    .AddCheck<HindsightRestHealthCheck>("hindsight");

// --- Consistent error responses across every endpoint. ---
builder.Services.AddProblemDetails();

// --- .NET 9's built-in OpenAPI document generation (see /openapi/v1.json once running). ---
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapOpenApi();

// Connect to Hindsight/Ollama once at startup. If it fails (e.g. Docker isn't running yet),
// log it and keep the web server up so /api/health can report what's wrong.
await app.Services.GetRequiredService<HindsightAgentService>().InitializeAsync();

app.MapChatEndpoints();
app.MapMemoryEndpoints();
app.MapHealthEndpoints();

app.Run();
