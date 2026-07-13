using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public static class AgentManagedSeedCustomizationMetadata
{
    private const string ManagedSeedVersionPropertyName = "managedSeedVersion";
    private const string CustomizationVersionPropertyName = "managedSeedCustomizationVersion";

    public static string MarkCustomized(string? configurationJson)
    {
        var root = ParseObject(configurationJson);
        if (!TryReadString(root, ManagedSeedVersionPropertyName, out var managedSeedVersion))
        {
            return root.ToJsonString();
        }

        root[CustomizationVersionPropertyName] = managedSeedVersion;
        return root.ToJsonString();
    }

    public static bool HasCurrentCustomization(string? configurationJson)
    {
        var root = ParseObject(configurationJson);
        return TryReadString(root, ManagedSeedVersionPropertyName, out var managedSeedVersion) &&
               TryReadString(root, CustomizationVersionPropertyName, out var customizationVersion) &&
               string.Equals(managedSeedVersion, customizationVersion, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryReadString(JsonObject root, string propertyName, out string value)
    {
        value = string.Empty;
        if (root[propertyName] is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<string>(out var parsedValue) ||
            string.IsNullOrWhiteSpace(parsedValue))
        {
            return false;
        }

        value = parsedValue;
        return true;
    }

    private static JsonObject ParseObject(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(configurationJson)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }
}
