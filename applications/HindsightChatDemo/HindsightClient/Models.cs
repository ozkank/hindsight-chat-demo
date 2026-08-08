using System.Text.Json.Serialization;

namespace HindsightChatDemo.HindsightClient;

/// <summary>
/// Response shape of Hindsight's GET /health endpoint. Deserialized with
/// System.Text.Json's default "ignore unknown members" behavior, so this only needs
/// to declare the fields we actually use -- Hindsight can add more without breaking us.
/// </summary>
public sealed record HindsightHealthStatus(
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("database")] string Database)
{
    public bool IsHealthy => Status == "healthy";
}

/// <summary>
/// One row from GET /v1/default/banks/{bankId}/memories/list. Hindsight's own schema
/// marks this "additionalProperties: true" (it's a loosely-typed dict server-side) --
/// these are the fields worth showing in a demo UI, not the full set.
/// </summary>
public sealed record MemoryRecord(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("context")] string? Context,
    [property: JsonPropertyName("fact_type")] string? FactType,
    [property: JsonPropertyName("date")] DateTimeOffset? Date,
    [property: JsonPropertyName("state")] string? State);

/// <summary>Envelope returned by the memories/list endpoint (pagination metadata + items).</summary>
public sealed record ListMemoriesResult(
    [property: JsonPropertyName("items")] IReadOnlyList<MemoryRecord> Items,
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("limit")] int Limit,
    [property: JsonPropertyName("offset")] int Offset);
