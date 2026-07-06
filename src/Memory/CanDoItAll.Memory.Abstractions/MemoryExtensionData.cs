using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Memory.Abstractions;

public sealed record MemoryExtensionData
{
    public static readonly string[] ReservedNamespaces =
    [
        "host.candoitall.",
        "native.cognitiveMemory.",
        "provider.vendor."
    ];

    public static readonly MemoryExtensionData Empty = new(new Dictionary<string, JsonElement>());

    [JsonConstructor]
    public MemoryExtensionData(IReadOnlyDictionary<string, JsonElement> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Values = values.ToDictionary(
            pair => MemoryProtocolGuard.EnsureExtensionKey(pair.Key, nameof(values)),
            pair => pair.Value.Clone(),
            StringComparer.Ordinal);
    }

    public IReadOnlyDictionary<string, JsonElement> Values { get; }

    public static MemoryExtensionData From(params (string Key, JsonElement Value)[] values) =>
        new(values.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal));
}
