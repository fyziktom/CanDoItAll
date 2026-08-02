using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;

namespace CanDoItAll.AgentFramework.Persistence;

internal static class ManagedCapabilitySeedMetadata
{
    internal const string PackVersionPropertyName = "managedSeedVersion";
    internal const string CapabilityVersionPropertyName = "managedCapabilityVersion";

    internal static CapabilityStableId CreateCapabilityVersion(string stableId)
        => CapabilityStableId.Create(stableId);

    internal static void Stamp(
        IDictionary<string, object?> configuration,
        string packVersion,
        CapabilityStableId capabilityVersion)
    {
        configuration[PackVersionPropertyName] = packVersion;
        configuration[CapabilityVersionPropertyName] = capabilityVersion.Value;
    }

    internal static bool TryReadCapabilityVersion(
        string? configurationJson,
        out CapabilityStableId capabilityVersion)
    {
        capabilityVersion = default;
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(CapabilityVersionPropertyName, out var versionElement) &&
                   versionElement.ValueKind == JsonValueKind.String &&
                   CapabilityStableId.TryCreate(versionElement.GetString(), out capabilityVersion);
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
