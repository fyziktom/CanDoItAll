using System.Text.Json;

namespace CanDoItAll.Memory.Abstractions;

public sealed record MemoryPayload(
    MemoryPayloadKind Kind,
    string? Text,
    JsonElement? Json)
{
    public static MemoryPayload FromText(string text) =>
        new(MemoryPayloadKind.Text, MemoryProtocolGuard.EnsureText(text, nameof(text)), null);

    public static MemoryPayload FromJson(JsonElement json) =>
        new(MemoryPayloadKind.Json, null, json.Clone());
}
