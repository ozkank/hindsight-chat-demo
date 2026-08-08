using System.Net.Http.Json;

namespace HindsightChatDemo.HindsightClient;

/// <summary>
/// Default <see cref="IHindsightRestClient"/> implementation. Registered as a typed
/// HttpClient (see Program.cs) -- BaseAddress and timeout come from that registration,
/// so this class only knows about relative paths and response shapes.
/// </summary>
public sealed class HindsightRestClient(HttpClient httpClient) : IHindsightRestClient
{
    public async Task<HindsightHealthStatus> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var status = await httpClient.GetFromJsonAsync<HindsightHealthStatus>("health", cancellationToken);
        return status ?? throw new InvalidOperationException("Hindsight /health returned an empty body.");
    }

    public async Task<ListMemoriesResult> ListMemoriesAsync(string bankId, int limit = 20, CancellationToken cancellationToken = default)
    {
        var path = $"v1/default/banks/{Uri.EscapeDataString(bankId)}/memories/list?limit={limit}";
        var result = await httpClient.GetFromJsonAsync<ListMemoriesResult>(path, cancellationToken);
        if (result is null)
        {
            return new ListMemoriesResult([], 0, limit, 0);
        }

        // The API doesn't document a guaranteed order; sort newest-first client-side so
        // the demo UI reliably shows "what just got retained" at the top.
        var newestFirst = result.Items.OrderByDescending(m => m.Date).ToList();
        return result with { Items = newestFirst };
    }
}
