using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

public static class ExecutionInvocationMetadata
{
    public const string MaxStructuredOutputRepairAttemptsMetadataKey = "agentMaxStructuredOutputRepairAttempts";
    public const string RequireStructuredOutputValidationMetadataKey = "agentRequireStructuredOutputValidation";
    public const string AllowedExternalTargetAliasesMetadataKey = "agentAllowedExternalTargetAliases";
    public const string ReadOnlyExternalTargetAliasesMetadataKey = "agentReadOnlyExternalTargetAliases";
    public const string ProcessCooperationModeMetadataKey = "agentProcessCooperationMode";
    public const string ProcessWorkspaceToolProfileMetadataKey = "agentProcessWorkspaceToolProfile";
    public const string ProcessCooperationSummaryMetadataKey = "agentProcessCooperationSummary";
    public const string ProcessBrowserToolsAllowedMetadataKey = "agentProcessBrowserToolsAllowed";
    public const string ProcessScaffoldToolOnlyMetadataKey = "agentProcessScaffoldToolOnly";
    public const string ProcessStepExecutionBoundaryMetadataKey = "agentProcessStepExecutionBoundary";
    public const string ProcessStepAllowedOperationsMetadataKey = "agentProcessStepAllowedOperations";
    public const string ProcessStepTargetScopeMetadataKey = "agentProcessStepTargetScope";
    public const string ProcessStepAllowsProductMutationMetadataKey = "agentProcessStepAllowsProductMutation";
    public const string ContextWorkspaceScopeMetadataKey = "agentContextWorkspaceScope";
    public const int DefaultGovernedRepairAttempts = 1;
    public const int MaxRepairAttempts = 2;
    private const string ContextWorkspaceScopeKindPropertyName = "kind";
    private const string ContextWorkspaceScopeKeyPropertyName = "key";

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

    public static string ApplyProcessCooperation(
        string? metadataJson,
        AgentProcessCooperationMetadata cooperationMetadata)
    {
        ArgumentNullException.ThrowIfNull(cooperationMetadata);

        var metadata = ParseObject(metadataJson);
        metadata[ProcessCooperationModeMetadataKey] = cooperationMetadata.CooperationMode.ToString();
        metadata[ProcessWorkspaceToolProfileMetadataKey] = AgentWorkspaceToolAccessProfiles.GetProfileKey(cooperationMetadata.WorkspaceToolProfile);
        metadata[ProcessCooperationSummaryMetadataKey] = cooperationMetadata.Summary.Trim();
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string ApplyContextWorkspaceScope(
        string? metadataJson,
        WorkspaceScopeDescriptor? scope)
    {
        var metadata = ParseObject(metadataJson);
        if (scope is null || scope.IsDefaultSandbox)
        {
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        metadata[ContextWorkspaceScopeMetadataKey] = new JsonObject
        {
            [ContextWorkspaceScopeKindPropertyName] = scope.Kind.ToString(),
            [ContextWorkspaceScopeKeyPropertyName] = scope.Key
        };
        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static string GroundPromptExternalTargetAliases(
        string? metadataJson,
        string? prompt,
        AgentWorkspaceToolAccessSettings? workspaceToolAccess)
    {
        var metadata = ParseObject(metadataJson);
        var accessSettings = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess ?? new AgentWorkspaceToolAccessSettings());
        if ((!accessSettings.CanReadFiles && !accessSettings.CanWriteFiles) ||
            string.IsNullOrWhiteSpace(prompt))
        {
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        var aliases = ExtractPromptExternalTargetAliases(prompt);
        if (aliases.Count == 0)
        {
            return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
        }

        var targetMetadataKey = accessSettings.CanWriteFiles &&
                                (!HasProcessBoundaryMetadata(metadata) || ResolveProcessAllowsProductMutation(metadata))
            ? AllowedExternalTargetAliasesMetadataKey
            : ReadOnlyExternalTargetAliasesMetadataKey;
        MergeExternalTargetAliases(
            metadata,
            targetMetadataKey,
            aliases);

        return metadata.ToJsonString(AgentOutputJson.SerializerOptions);
    }

    public static IReadOnlyList<string> ExtractPromptExternalTargetAliases(string? prompt)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return [];
        }

        var aliases = new List<string>();
        for (var index = 0; index < prompt.Length; index++)
        {
            if (!IsPathCandidateStart(prompt, index))
            {
                continue;
            }

            var candidate = ReadPathCandidate(prompt, index, out var nextIndex);
            var alias = AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(candidate);
            if (!string.IsNullOrWhiteSpace(alias))
            {
                aliases.Add(alias);
            }

            index = Math.Max(index, nextIndex - 1);
        }

        return aliases
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    public static IReadOnlyList<string> ResolveAllowedExternalTargetAliases(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return ResolveExternalTargetAliases(run, AllowedExternalTargetAliasesMetadataKey);
    }

    public static IReadOnlyList<string> ResolveReadOnlyExternalTargetAliases(
        ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return ResolveExternalTargetAliases(run, ReadOnlyExternalTargetAliasesMetadataKey);
    }

    public static AgentProcessCooperationMode? ResolveProcessCooperationMode(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return null;
        }

        var value = TryReadString(run.MetadataJson, ProcessCooperationModeMetadataKey);
        return Enum.TryParse<AgentProcessCooperationMode>(value, ignoreCase: true, out var parsed)
            ? parsed
            : null;
    }

    public static AgentWorkspaceToolProfileKind? ResolveProcessWorkspaceToolProfile(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return null;
        }

        var value = TryReadString(run.MetadataJson, ProcessWorkspaceToolProfileMetadataKey);
        return AgentWorkspaceToolAccessProfiles.TryParseProfileKey(value, out var parsed)
            ? parsed
            : null;
    }

    public static string ResolveProcessCooperationSummary(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return IsTrustedGovernedProcessRun(run)
            ? TryReadString(run.MetadataJson, ProcessCooperationSummaryMetadataKey)
            : string.Empty;
    }

    public static bool ResolveProcessBrowserToolsAllowed(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return !IsTrustedGovernedProcessRun(run) ||
               TryReadBoolean(run.MetadataJson, ProcessBrowserToolsAllowedMetadataKey) != false;
    }

    public static bool ResolveProcessScaffoldToolOnly(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return IsTrustedGovernedProcessRun(run) &&
               TryReadBoolean(run.MetadataJson, ProcessScaffoldToolOnlyMetadataKey) == true;
    }

    public static bool ResolveProcessAllowsProductMutation(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        if (!IsTrustedGovernedProcessRun(run))
        {
            return true;
        }

        return ResolveProcessAllowsProductMutation(ParseObject(run.MetadataJson));
    }

    public static WorkspaceScopeDescriptor? ResolveContextWorkspaceScope(ExecutionRunRecord run)
    {
        ArgumentNullException.ThrowIfNull(run);

        return IsTrustedGovernedProcessRun(run)
            ? ResolveContextWorkspaceScope(run.MetadataJson)
            : null;
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

    private static bool ResolveProcessAllowsProductMutation(JsonObject metadata)
    {
        if (metadata[ProcessStepAllowsProductMutationMetadataKey] is JsonValue value &&
            value.TryGetValue<bool>(out var allowsProductMutation))
        {
            return allowsProductMutation;
        }

        if (metadata[ProcessStepAllowedOperationsMetadataKey] is JsonArray operations &&
            operations
                .Select(item => item?.GetValue<string>())
                .Any(item => string.Equals(item, "MutateProductTarget", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        if (metadata[ProcessStepTargetScopeMetadataKey] is JsonValue scopeValue &&
            scopeValue.TryGetValue<string>(out var scope))
        {
            return scope.Contains("Mutable", StringComparison.OrdinalIgnoreCase);
        }

        if (metadata[ProcessStepExecutionBoundaryMetadataKey] is JsonValue boundaryValue &&
            boundaryValue.TryGetValue<string>(out var boundary))
        {
            return string.Equals(boundary, "ProductMutation", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(boundary, "Recovery", StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool HasProcessBoundaryMetadata(JsonObject metadata)
    {
        return metadata.ContainsKey(ProcessStepAllowsProductMutationMetadataKey) ||
               metadata.ContainsKey(ProcessStepAllowedOperationsMetadataKey) ||
               metadata.ContainsKey(ProcessStepTargetScopeMetadataKey) ||
               metadata.ContainsKey(ProcessStepExecutionBoundaryMetadataKey);
    }

    private static IReadOnlyList<string> ResolveExternalTargetAliases(
        ExecutionRunRecord run,
        string metadataKey)
    {
        if (string.IsNullOrWhiteSpace(run.MetadataJson))
        {
            return [];
        }

        try
        {
            using var document = JsonDocument.Parse(run.MetadataJson);
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

    private static WorkspaceScopeDescriptor? ResolveContextWorkspaceScope(string? metadataJson)
    {
        if (string.IsNullOrWhiteSpace(metadataJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(metadataJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty(ContextWorkspaceScopeMetadataKey, out var scopeElement) ||
                scopeElement.ValueKind != JsonValueKind.Object ||
                !scopeElement.TryGetProperty(ContextWorkspaceScopeKindPropertyName, out var kindElement) ||
                kindElement.ValueKind != JsonValueKind.String)
            {
                return null;
            }

            var kindValue = kindElement.GetString();
            if (!Enum.TryParse<WorkspaceScopeKind>(kindValue, ignoreCase: true, out var kind))
            {
                return null;
            }

            var key = scopeElement.TryGetProperty(ContextWorkspaceScopeKeyPropertyName, out var keyElement) &&
                      keyElement.ValueKind == JsonValueKind.String
                ? keyElement.GetString()
                : null;

            return kind == WorkspaceScopeKind.Sandbox && string.IsNullOrWhiteSpace(key)
                ? WorkspaceScopeDescriptor.Sandbox
                : new WorkspaceScopeDescriptor(kind, key);
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException)
        {
            return null;
        }
    }

    private static void MergeExternalTargetAliases(
        JsonObject metadata,
        string metadataKey,
        IReadOnlyList<string> aliases)
    {
        var mergedAliases = new List<string>();
        if (metadata[metadataKey] is JsonArray existingAliases)
        {
            mergedAliases.AddRange(existingAliases
                .Select(item => item?.GetValue<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(item))
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Cast<string>());
        }

        mergedAliases.AddRange(aliases);
        metadata[metadataKey] = new JsonArray(
            mergedAliases
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Select(alias => JsonValue.Create(alias))
                .ToArray());
    }

    private static bool IsPathCandidateStart(string value, int index)
    {
        if (index < 0 || index >= value.Length)
        {
            return false;
        }

        if (index + 2 < value.Length &&
            char.IsLetter(value[index]) &&
            value[index + 1] == ':' &&
            (value[index + 2] == '\\' || value[index + 2] == '/'))
        {
            return index == 0 || !char.IsLetterOrDigit(value[index - 1]);
        }

        return value.AsSpan(index).StartsWith("external-target/", StringComparison.OrdinalIgnoreCase) &&
               (index == 0 || !char.IsLetterOrDigit(value[index - 1]));
    }

    private static string ReadPathCandidate(string value, int startIndex, out int nextIndex)
    {
        var terminator = startIndex > 0 && IsQuote(value[startIndex - 1])
            ? value[startIndex - 1]
            : '\0';

        var index = startIndex;
        while (index < value.Length)
        {
            var current = value[index];
            if (terminator != '\0')
            {
                if (current == terminator)
                {
                    break;
                }
            }
            else if (char.IsWhiteSpace(current) ||
                     current is '"' or '\'' or '`' or '<' or '>' or '|' or '?' or '*' or ',' or ';' or ')' or ']' or '}')
            {
                break;
            }

            index++;
        }

        nextIndex = index;
        return value[startIndex..index].Trim().TrimEnd('.', ',', ';', ':', ')', ']', '}');
    }

    private static bool IsQuote(char value)
        => value is '"' or '\'' or '`';

    private static bool IsGovernedMachineCriticalRun(ExecutionRunRecord run)
    {
        return string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(run.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
    }

    private static bool IsTrustedGovernedProcessRun(ExecutionRunRecord run)
    {
        return string.Equals(run.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(run.RequestedByKind, "system", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(run.ProcessRunId) &&
               !string.IsNullOrWhiteSpace(run.ProcessStepId);
    }
}
