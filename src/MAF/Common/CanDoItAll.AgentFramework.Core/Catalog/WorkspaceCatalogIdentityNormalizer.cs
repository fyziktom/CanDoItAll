using System.Text;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

internal static class WorkspaceCatalogIdentityNormalizer
{
    public static string NormalizeTemplateKey(string? templateKey, string? fallbackName)
    {
        var normalized = NormalizeComparableKey(string.IsNullOrWhiteSpace(templateKey) ? fallbackName : templateKey);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Template key must contain at least one letter or digit.");
        }

        return normalized;
    }

    public static string NormalizeCapabilityKey(string? capabilityKey)
    {
        var normalized = NormalizeComparableKey(capabilityKey);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            throw new InvalidOperationException("Capability key must contain at least one letter or digit.");
        }

        return normalized;
    }

    public static string GetAgentTemplateIdentity(AgentDefinition agent)
        => NormalizeTemplateKey(agent.TemplateKey, agent.Name);

    public static string GetProviderIdentityKey(ProviderProfile provider)
        => $"{provider.Kind}:{NormalizeComparableKey(provider.Name)}";

    public static string GetCapabilityIdentityKey(CapabilityCatalogItem capability)
        => $"{capability.Kind}:{NormalizeCapabilityKey(capability.Key)}";

    public static string NormalizeComparableKey(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(value.Length);
        var pendingSeparator = false;

        foreach (var character in value.Trim())
        {
            if (char.IsLetterOrDigit(character))
            {
                if (pendingSeparator && builder.Length > 0)
                {
                    builder.Append('-');
                }

                builder.Append(char.ToLowerInvariant(character));
                pendingSeparator = false;
            }
            else if (builder.Length > 0)
            {
                pendingSeparator = true;
            }
        }

        return builder.ToString();
    }
}
