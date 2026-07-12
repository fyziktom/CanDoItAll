using System.Net.Http;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public sealed partial class CapabilityProofService : ICapabilityProofService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };
    private static readonly ProviderProfileService ProviderFeatureService = new();

    private static readonly HashSet<string> BuiltInToolKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "provider-health",
        "agent-package-export",
        "provider-native-code-interpreter",
        "provider-native-file-search",
        "provider-native-web-search",
        "workspace-search",
        "workspace-read-file",
        "workspace-list-directory",
        "workspace-list-files",
        "workspace-execution-boundary",
        "workspace-stat-path",
        "workspace-hash-path",
        "workspace-create-directory",
        "workspace-write-file",
        "workspace-append-file",
        "workspace-copy-path",
        "workspace-move-path",
        "workspace-delete-path",
        "workspace-zip-path",
        "workspace-unzip-archive",
        "workspace-diff-text",
        "workspace-git-status",
        "workspace-git-diff",
        "workspace-dotnet-restore",
        "workspace-dotnet-build",
        "workspace-dotnet-test",
        "workspace-dotnet-new",
        "workspace-python-run-file",
        "workspace-pwsh-run-script",
        "workspace-convert-document",
        "workspace-inspect-spreadsheet",
        "workspace-spreadsheet-summary",
        "workspace-read-spreadsheet-cell",
        "workspace-read-spreadsheet-range",
        "workspace-write-spreadsheet",
        "workspace-spreadsheet-function-catalog",
        "workspace-plugin"
    };

    public async Task<CapabilityVerificationResult> VerifyAsync(
        AgentDefinition agent,
        ProviderProfile? provider,
        CapabilityCatalogItem capability,
        CancellationToken cancellationToken = default)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var notes = new List<string>();

        if (!agent.Capabilities.Any(item => item.CapabilityId == capability.Id))
        {
            return Failed("Capability is not attached to the selected agent.", checkedAt);
        }

        notes.Add($"Capability '{capability.Name}' is attached to agent '{agent.Name}'.");

        return capability.Kind switch
        {
            CapabilityKind.Skill => await VerifySkillAsync(capability, notes, checkedAt, cancellationToken),
            CapabilityKind.Tool => await VerifyToolLikeCapabilityAsync(agent, provider, capability, notes, checkedAt, cancellationToken),
            CapabilityKind.Plugin => await VerifyPluginCapabilityAsync(agent, provider, capability, notes, checkedAt, cancellationToken),
            CapabilityKind.McpServer => await VerifyMcpCapabilityAsync(agent, provider, capability, notes, checkedAt, cancellationToken),
            CapabilityKind.Rag => VerifyRagCapability(capability, notes, checkedAt),
            CapabilityKind.AiContext => VerifyAiContextCapability(capability, notes, checkedAt),
            CapabilityKind.Memory => Failed(LegacyMemoryCapabilityPolicy.BuildDiagnostic(capability.Name), checkedAt),
            _ => PendingReview("Capability kind is recorded, but this sandbox does not have a verification rule for it yet.", checkedAt)
        };
    }

    private static string? TryReadConfigurationString(string configurationJson, params string[] propertyPath)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            var current = document.RootElement;
            foreach (var segment in propertyPath)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    return null;
                }
            }

            return current.ValueKind == JsonValueKind.String
                ? current.GetString()
                : current.ToString();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool? TryReadConfigurationBoolean(string configurationJson, params string[] propertyPath)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            var current = document.RootElement;
            foreach (var segment in propertyPath)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    return null;
                }
            }

            return current.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(current.GetString(), out var value) => value,
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static bool HasNonEmptyConfigurationObject(string configurationJson, params string[] propertyPath)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            var current = document.RootElement;
            foreach (var segment in propertyPath)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    return false;
                }
            }

            return current.ValueKind == JsonValueKind.Object && current.EnumerateObject().Any();
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static IReadOnlyDictionary<string, string> ReadConfigurationStringDictionary(string configurationJson, params string[] propertyPath)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            var current = document.RootElement;
            foreach (var segment in propertyPath)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                }
            }

            if (current.ValueKind != JsonValueKind.Object)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var property in current.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(property.Value.GetString()))
                {
                    values[property.Name] = property.Value.GetString()!;
                }
            }

            return values;
        }
        catch (JsonException)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static bool TryResolveConfiguredPath(
        CapabilityCatalogItem capability,
        string configProperty,
        out string filePath)
    {
        var configuredPath = TryReadConfigurationString(capability.ConfigurationJson, configProperty);
        return TryResolveFilePath(configuredPath ?? capability.EndpointOrPath, out filePath);
    }

    private static IReadOnlyList<string> ReadConfigurationStringArray(string configurationJson, params string[] propertyPath)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            var current = document.RootElement;
            foreach (var segment in propertyPath)
            {
                if (current.ValueKind != JsonValueKind.Object || !current.TryGetProperty(segment, out current))
                {
                    return [];
                }
            }

            if (current.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return current.EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static CapabilityVerificationResult Verified(IEnumerable<string> notes, DateTimeOffset checkedAt)
    {
        return new CapabilityVerificationResult(
            CapabilityProofStatus.Verified,
            string.Join(" ", notes),
            checkedAt);
    }

    private static CapabilityVerificationResult Verified(string notes, DateTimeOffset checkedAt)
    {
        return new CapabilityVerificationResult(CapabilityProofStatus.Verified, notes, checkedAt);
    }

    private static CapabilityVerificationResult Failed(string notes, DateTimeOffset checkedAt)
    {
        return new CapabilityVerificationResult(CapabilityProofStatus.Failed, notes, checkedAt);
    }

    private static CapabilityVerificationResult PendingReview(string notes, DateTimeOffset checkedAt)
    {
        return new CapabilityVerificationResult(CapabilityProofStatus.PendingReview, notes, checkedAt);
    }
}
