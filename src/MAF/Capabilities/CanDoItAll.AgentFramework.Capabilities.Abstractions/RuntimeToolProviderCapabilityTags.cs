namespace CanDoItAll.AgentFramework.Capabilities.Abstractions;

public static class RuntimeToolProviderCapabilityTags
{
    public const string RuntimeProviderTagValue = "runtime-provider";

    public static CapabilityTag RuntimeProviderTag { get; } = CapabilityTag.Create(RuntimeProviderTagValue);

    public static CapabilityTag CreateProviderKeyTag(string providerKey)
        => CapabilityTag.Create(CreateProviderKeyTagValue(providerKey));

    public static string CreateProviderKeyTagValue(string providerKey)
        => RuntimeProviderTagValue + "-" + NormalizeKebabSegment(providerKey);

    public static ImplementationKey CreateToolImplementationKey(string providerKey, RuntimeToolName runtimeToolName)
        => ImplementationKey.Create($"runtime-provider.{NormalizeImplementationSegment(providerKey)}.{runtimeToolName.Value}");

    private static string NormalizeKebabSegment(string value)
        => NormalizeIdentifierSegment(value, '-');

    private static string NormalizeImplementationSegment(string value)
        => NormalizeIdentifierSegment(value, '.');

    private static string NormalizeIdentifierSegment(string value, char separator)
    {
        var normalized = new List<char>();
        var lastWasSeparator = false;
        foreach (var character in value.Trim())
        {
            if (char.IsAsciiLetterOrDigit(character))
            {
                normalized.Add(char.ToLowerInvariant(character));
                lastWasSeparator = false;
                continue;
            }

            if (lastWasSeparator || normalized.Count == 0)
            {
                continue;
            }

            normalized.Add(separator);
            lastWasSeparator = true;
        }

        while (normalized.Count > 0 && normalized[^1] == separator)
        {
            normalized.RemoveAt(normalized.Count - 1);
        }

        return normalized.Count == 0
            ? "unknown"
            : new string(normalized.ToArray());
    }
}
