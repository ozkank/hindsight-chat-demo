using System.Text.Json;
using HindsightChatDemo.Models;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HindsightChatDemo.Endpoints;

/// <summary>
/// Wires ASP.NET Core's built-in Health Checks middleware to /api/health, using a
/// custom response writer so the JSON shape stays exactly what the frontend and
/// DEMO.md's checklist already expect ({ healthy, api: {...}, hindsight: {...} })
/// instead of the framework's default plain-text "Healthy"/"Unhealthy" output.
/// </summary>
public static class HealthEndpoints
{
    // Matches ASP.NET Core's own default minimal-API JSON casing (camelCase), which
    // Results.Ok(...) applies automatically but this hand-written writer would not
    // unless told to -- keeps /api/health's { healthy, api, hindsight } shape consistent
    // with every other endpoint and with what DEMO.md's checklist and the UI expect.
    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web);

    public static void MapHealthEndpoints(this WebApplication app)
    {
        app.MapHealthChecks("/api/health", new HealthCheckOptions
        {
            ResponseWriter = WriteResponseAsync,
        });
    }

    private static Task WriteResponseAsync(HttpContext context, HealthReport report)
    {
        var api = ToDependency(report, "agent");
        var hindsight = ToDependency(report, "hindsight");

        var response = new HealthResponse
        {
            Healthy = report.Status == HealthStatus.Healthy,
            Api = api,
            Hindsight = hindsight,
        };

        context.Response.ContentType = "application/json";
        return context.Response.WriteAsync(JsonSerializer.Serialize(response, ResponseJsonOptions));
    }

    private static HealthDependency ToDependency(HealthReport report, string checkName)
    {
        if (!report.Entries.TryGetValue(checkName, out var entry))
        {
            return new HealthDependency { Up = false, Detail = $"health check '{checkName}' not registered" };
        }

        return new HealthDependency
        {
            Up = entry.Status == HealthStatus.Healthy,
            Detail = entry.Exception?.Message ?? entry.Description,
        };
    }
}
