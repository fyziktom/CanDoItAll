using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text.Json;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static IReadOnlyList<string> ResolveAllowedExternalTargetAliases(
        ProcessAutomationExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return ResolveExternalTargetAliases(
            run.MetadataJson,
            ExecutionInvocationMetadata.AllowedExternalTargetAliasesMetadataKey);
    }

    private static bool ResolveProcessBrowserToolsAllowed(
        ProcessAutomationExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return !IsTrustedGovernedProcessRun(run) ||
               TryReadBoolean(run.MetadataJson, ExecutionInvocationMetadata.ProcessBrowserToolsAllowedMetadataKey) != false;
    }

    private static bool ResolveProcessAllowsProductMutation(
        ProcessAutomationExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return true;
        }

        if (TryReadBoolean(run.MetadataJson, ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey) is { } allowsProductMutation)
        {
            return allowsProductMutation;
        }

        if (ReadStringArray(run.MetadataJson, ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey)
            .Any(item => string.Equals(item, "MutateProductTarget", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        var scope = TryReadString(run.MetadataJson, ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey);
        if (scope.Contains("Mutable", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var boundary = TryReadString(run.MetadataJson, ExecutionInvocationMetadata.ProcessStepExecutionBoundaryMetadataKey);
        return string.Equals(boundary, "ProductMutation", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(boundary, "Recovery", StringComparison.OrdinalIgnoreCase);
    }

    private static IReadOnlyList<string> ResolveExternalTargetAliases(
        string? metadataJson,
        string metadataKey)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(metadataKey, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return value
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias)
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .Where(item => item.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool? TryReadBoolean(
        string? metadataJson,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(propertyName, out var value))
            {
                return null;
            }

            return value.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                _ => null
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string TryReadString(
        string? metadataJson,
        string propertyName)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return string.Empty;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(propertyName, out var value) &&
                   value.ValueKind == JsonValueKind.String
                ? value.GetString()?.Trim() ?? string.Empty
                : string.Empty;
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    private static IReadOnlyList<string> ReadStringArray(
        string? metadataJson,
        string metadataKey)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(metadataKey, out var value) ||
                value.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return value
                .EnumerateArray()
                .Where(item => item.ValueKind == JsonValueKind.String)
                .Select(item => item.GetString()?.Trim())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static bool IsTrustedGovernedProcessRun(ProcessAutomationExecutionRunRecord run)
    {
        return string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(run.RequestedByKind, "system", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(run.ProcessRunId) &&
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
    }
}
