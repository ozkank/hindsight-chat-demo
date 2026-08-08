using HindsightChatDemo.HindsightClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace HindsightChatDemo.HealthChecks;

/// <summary>
/// Reports whether Hindsight itself is reachable and healthy, using the typed REST
/// client rather than a one-off HttpClient call -- the same client the memory-viewer
/// endpoint uses.
/// </summary>
public sealed class HindsightRestHealthCheck(IHindsightRestClient client) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var status = await client.GetHealthAsync(cancellationToken);
            return status.IsHealthy
                ? HealthCheckResult.Healthy($"database: {status.Database}")
                : HealthCheckResult.Unhealthy($"status: {status.Status}");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(ex.Message, ex);
        }
    }
}
