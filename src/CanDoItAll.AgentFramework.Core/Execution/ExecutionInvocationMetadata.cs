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

        MergeExternalTargetAliases(
            metadata,
            accessSettings.CanWriteFiles
                ? AllowedExternalTargetAliasesMetadataKey
                : ReadOnlyExternalTargetAliasesMetadataKey,
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
                .Select(item => item!.Replace('\\', '/').Trim().TrimEnd('/', '.', ',', ';', ':', ')', ']', '}'))
                .Where(item => item.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (JsonException)
        {
            return [];
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
}
