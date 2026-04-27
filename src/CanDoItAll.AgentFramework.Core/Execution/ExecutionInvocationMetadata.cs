using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class ExecutionInvocationMetadata
{
    public const string MaxStructuredOutputRepairAttemptsMetadataKey = "agentMaxStructuredOutputRepairAttempts";
    public const string RequireStructuredOutputValidationMetadataKey = "agentRequireStructuredOutputValidation";
    public const int DefaultGovernedRepairAttempts = 1;
    public const int MaxRepairAttempts = 2;

    public static string Build(
        string? metadataJson,
        ExecutionInvocationPolicy? policy)
    {
        var metadata = ParseObject(metadataJson);
        if (policy is null)
        {
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        if (policy.FinalizerMode.HasValue)
        {
            metadata[AgentFinalizerPolicies.FinalizerModeMetadataKey] = AgentFinalizerPolicies.FormatMode(policy.FinalizerMode.Value);
        }

        if (policy.MaxStructuredOutputRepairAttempts.HasValue)
        {
            metadata[MaxStructuredOutputRepairAttemptsMetadataKey] = ClampRepairAttempts(policy.MaxStructuredOutputRepairAttempts.Value);
        }

        metadata[RequireStructuredOutputValidationMetadataKey] = policy.RequireStructuredOutputValidation;
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static int ResolveMaxStructuredOutputRepairAttempts(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        var configured = TryReadInt32(run.MetadataJson, MaxStructuredOutputRepairAttemptsMetadataKey);
        if (configured.HasValue)
        {
            return ClampRepairAttempts(configured.Value);
        }

        return IsGovernedMachineCriticalRun(run)
            ? DefaultGovernedRepairAttempts
            : 0;
    }

    public static bool ResolveRequireStructuredOutputValidation(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return TryReadBoolean(run.MetadataJson, RequireStructuredOutputValidationMetadataKey) ?? true;
    }

    private static JsonObject ParseObject(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return [];
        }

        try
        {
            return JsonNode.Parse(metadataJson) as JsonObject ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static int ClampRepairAttempts(int value)
    {
        return Math.Clamp(value, 0, MaxRepairAttempts);
    }

    private static int? TryReadInt32(
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
            return document.RootElement.ValueKind == JsonValueKind.Object &&
                   document.RootElement.TryGetProperty(propertyName, out var value) &&
                   value.TryGetInt32(out var parsed)
                ? parsed
                : null;
        }
        catch (JsonException)
        {
            return null;
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

    private static bool IsGovernedMachineCriticalRun(ExecutionRunRecord run)
    {
        return string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(run.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
    }
}
