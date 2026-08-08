using HindsightChatDemo.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HindsightChatDemo.HealthChecks;

/// <summary>Reports whether the MCP-backed chat agent finished initializing successfully.</summary>
public sealed class AgentHealthCheck(HindsightAgentService agentService) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var result = agentService.IsReady
            ? HealthCheckResult.Healthy("agent ready")
            : HealthCheckResult.Unhealthy(agentService.InitError ?? "agent not initialized");

        return Task.FromResult(result);
    }
}
