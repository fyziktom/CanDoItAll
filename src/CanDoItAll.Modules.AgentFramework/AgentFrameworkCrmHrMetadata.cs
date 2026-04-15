using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Modules.CrmHr;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class AgentFrameworkCrmHrMetadataModel
{
    public Guid PartyId { get; set; }

    public AiExecutionMode ExecutionMode { get; set; } = AiExecutionMode.Remote;

    public List<AiCapabilityEditorModel> Capabilities { get; set; } = [];
}

internal static class AgentFrameworkCrmHrMetadata
{
    private const string RootPropertyName = "crmHr";
    private const string PartyIdPropertyName = "partyId";
    private const string ExecutionModePropertyName = "executionMode";
    private const string CapabilitiesPropertyName = "capabilities";
    private const string SourcePropertyName = "source";

    public static AgentFrameworkCrmHrMetadataModel? Read(
        string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return null;
        }

        try
        {
            var root = JsonNode.Parse(configurationJson)?.AsObject();
            var crmHr = root?[RootPropertyName]?.AsObject();
            if (crmHr is null)
            {
                return null;
            }

            var metadata = new AgentFrameworkCrmHrMetadataModel();
            if (crmHr[PartyIdPropertyName] is JsonValue partyValue &&
                partyValue.TryGetValue<string>(out var partyText) &&
                Guid.TryParse(partyText, out var partyId))
            {
                metadata.PartyId = partyId;
            }

            if (crmHr[ExecutionModePropertyName] is JsonValue executionModeValue &&
                executionModeValue.TryGetValue<string>(out var executionModeText) &&
                Enum.TryParse<AiExecutionMode>(executionModeText, true, out var executionMode))
            {
                metadata.ExecutionMode = executionMode;
            }

            if (crmHr[CapabilitiesPropertyName] is JsonArray capabilitiesArray)
            {
                metadata.Capabilities = capabilitiesArray
                    .Select(item => item?.Deserialize<AiCapabilityEditorModel>())
                    .Where(item => item is not null)
                    .Cast<AiCapabilityEditorModel>()
                    .ToList();
            }

            return metadata.PartyId == Guid.Empty
                ? null
                : metadata;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static string Write(
        string? configurationJson,
        Guid partyId,
        AiExecutionMode executionMode,
        IReadOnlyList<AiCapabilityEditorModel> capabilities)
    {
        var root = ParseObject(configurationJson);
        var crmHr = root[RootPropertyName] as JsonObject ?? new JsonObject();
        crmHr[PartyIdPropertyName] = partyId.ToString("D");
        crmHr[ExecutionModePropertyName] = executionMode.ToString();
        crmHr[SourcePropertyName] = "crm-hr";
        crmHr[CapabilitiesPropertyName] = JsonSerializer.SerializeToNode(
            capabilities
                .Select(NormalizeCapability)
                .Where(HasCapabilityContent)
                .ToList());
        root[RootPropertyName] = crmHr;
        return root.ToJsonString();
    }

    public static string EnsurePartyTag(
        IReadOnlyList<string> tags,
        Guid partyId)
    {
        var normalizedTags = tags
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .ToList();
        var partyTag = BuildPartyTag(partyId);
        if (!normalizedTags.Contains("crm-hr", StringComparer.OrdinalIgnoreCase))
        {
            normalizedTags.Add("crm-hr");
        }

        if (!normalizedTags.Contains(partyTag, StringComparer.OrdinalIgnoreCase))
        {
            normalizedTags.Add(partyTag);
        }

        return string.Join(", ", normalizedTags.Distinct(StringComparer.OrdinalIgnoreCase));
    }

    public static string BuildPartyTag(
        Guid partyId)
    {
        return $"party-{partyId:N}";
    }

    public static string BuildInstructions(
        string? existingInstructions,
        string displayName,
        string notes,
        IReadOnlyList<AiCapabilityEditorModel> capabilities)
    {
        if (!string.IsNullOrWhiteSpace(existingInstructions))
        {
            return existingInstructions.Trim();
        }

        var builder = new StringBuilder();
        builder.Append("You are the technical execution profile for ")
            .Append(displayName)
            .Append('.');

        if (!string.IsNullOrWhiteSpace(notes))
        {
            builder.AppendLine()
                .Append("Business notes: ")
                .Append(notes.Trim());
        }

        var normalizedCapabilities = capabilities
            .Select(NormalizeCapability)
            .Where(HasCapabilityContent)
            .ToList();
        if (normalizedCapabilities.Count > 0)
        {
            builder.AppendLine()
                .Append("Business-declared capabilities:");
            foreach (var capability in normalizedCapabilities)
            {
                builder.AppendLine()
                    .Append("- ")
                    .Append(capability.Name);
                if (!string.IsNullOrWhiteSpace(capability.Scope))
                {
                    builder.Append(": ")
                        .Append(capability.Scope);
                }
            }
        }

        return builder.ToString().Trim();
    }

    public static IReadOnlyList<AiCapabilityEditorModel> NormalizeCapabilities(
        IReadOnlyList<AiCapabilityEditorModel> capabilities)
    {
        return capabilities
            .Select(NormalizeCapability)
            .Where(HasCapabilityContent)
            .ToList();
    }

    private static AiCapabilityEditorModel NormalizeCapability(
        AiCapabilityEditorModel capability)
    {
        return new AiCapabilityEditorModel
        {
            Name = capability.Name.Trim(),
            Scope = capability.Scope.Trim(),
            ToolAccess = capability.ToolAccess.Trim(),
            Limitations = capability.Limitations.Trim(),
            Notes = capability.Notes.Trim()
        };
    }

    private static bool HasCapabilityContent(
        AiCapabilityEditorModel capability)
    {
        return !string.IsNullOrWhiteSpace(capability.Name) ||
               !string.IsNullOrWhiteSpace(capability.Scope) ||
               !string.IsNullOrWhiteSpace(capability.ToolAccess) ||
               !string.IsNullOrWhiteSpace(capability.Limitations) ||
               !string.IsNullOrWhiteSpace(capability.Notes);
    }

    private static JsonObject ParseObject(
        string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        try
        {
            return JsonNode.Parse(json)?.AsObject() ?? new JsonObject();
        }
        catch (JsonException)
        {
            return new JsonObject();
        }
    }
}
