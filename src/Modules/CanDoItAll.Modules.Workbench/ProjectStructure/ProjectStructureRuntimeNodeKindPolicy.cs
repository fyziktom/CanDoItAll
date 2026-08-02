using System.Text.Json;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectStructureRuntimeNodeKindPolicy
{
    public static bool TryValidateAndApply(
        ProjectObjectType objectType,
        string? objectSubtype,
        string metadataJson,
        ProjectObjectMetadataEnvelope metadata,
        out string validationMessage)
    {
        validationMessage = string.Empty;
        return objectType switch
        {
            ProjectObjectType.Script => TryValidateAndApplyScriptKind(
                objectSubtype,
                metadataJson,
                metadata.Script,
                out validationMessage),
            ProjectObjectType.Environment => TryValidateAndApplyEnvironmentKind(
                objectSubtype,
                metadataJson,
                metadata.Environment,
                out validationMessage),
            ProjectObjectType.Infrastructure => TryValidateAndApplyInfrastructureKind(
                objectSubtype,
                metadataJson,
                metadata.Infrastructure,
                out validationMessage),
            _ => true
        };
    }

    private static bool TryValidateAndApplyScriptKind(
        string? objectSubtype,
        string metadataJson,
        ProjectScriptMetadata? metadata,
        out string validationMessage)
    {
        validationMessage = string.Empty;
        if (metadata is null)
        {
            return true;
        }

        var expectedKind = ProjectNodeKindRegistry.ResolveScriptKind(objectSubtype);
        if (HasProperty(metadataJson, "script", "scriptKind") && metadata.ScriptKind != expectedKind)
        {
            validationMessage = CreateMismatchMessage(
                "metadata.script.scriptKind",
                metadata.ScriptKind,
                objectSubtype,
                expectedKind);
            return false;
        }

        metadata.ScriptKind = expectedKind;
        return true;
    }

    private static bool TryValidateAndApplyEnvironmentKind(
        string? objectSubtype,
        string metadataJson,
        ProjectEnvironmentMetadata? metadata,
        out string validationMessage)
    {
        validationMessage = string.Empty;
        if (metadata is null)
        {
            return true;
        }

        var expectedKind = ProjectNodeKindRegistry.ResolveEnvironmentKind(objectSubtype);
        if (HasProperty(metadataJson, "environment", "environmentKind") && metadata.EnvironmentKind != expectedKind)
        {
            validationMessage = CreateMismatchMessage(
                "metadata.environment.environmentKind",
                metadata.EnvironmentKind,
                objectSubtype,
                expectedKind);
            return false;
        }

        metadata.EnvironmentKind = expectedKind;
        return true;
    }

    private static bool TryValidateAndApplyInfrastructureKind(
        string? objectSubtype,
        string metadataJson,
        ProjectInfrastructureMetadata? metadata,
        out string validationMessage)
    {
        validationMessage = string.Empty;
        if (metadata is null)
        {
            return true;
        }

        var expectedKind = ProjectNodeKindRegistry.ResolveInfrastructureKind(objectSubtype);
        if (HasProperty(metadataJson, "infrastructure", "infrastructureKind") && metadata.InfrastructureKind != expectedKind)
        {
            validationMessage = CreateMismatchMessage(
                "metadata.infrastructure.infrastructureKind",
                metadata.InfrastructureKind,
                objectSubtype,
                expectedKind);
            return false;
        }

        metadata.InfrastructureKind = expectedKind;
        return true;
    }

    private static bool HasProperty(string metadataJson, string familyName, string propertyName)
    {
        using var document = JsonDocument.Parse(metadataJson);
        return document.RootElement.ValueKind == JsonValueKind.Object &&
               TryGetProperty(document.RootElement, familyName, out var family) &&
               family.ValueKind == JsonValueKind.Object &&
               TryGetProperty(family, propertyName, out _);
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (property.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    private static string CreateMismatchMessage<TKind>(
        string metadataPath,
        TKind actualKind,
        string? objectSubtype,
        TKind expectedKind)
        where TKind : struct, Enum
        => $"{metadataPath} '{actualKind}' does not match objectSubtype '{objectSubtype?.Trim() ?? string.Empty}', which requires '{expectedKind}'. Repair the subtype and metadata kind together.";
}
