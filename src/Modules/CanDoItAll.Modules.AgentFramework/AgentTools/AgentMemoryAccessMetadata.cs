using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class AgentMemoryAccessSettings
{
    public bool CanUseMemoryTools { get; set; }

    public bool CanUseContextContributions { get; set; }

    public bool RequireContextContributions { get; set; }

    public bool AllowAsyncContextContributions { get; set; }

    public bool CanIngestSources { get; set; }

    public string PreferredProviderInstanceId { get; set; } = string.Empty;

    public string DefaultProviderInstanceId { get; set; } = string.Empty;

    public IReadOnlyList<string> AllowedProviderInstanceIds { get; set; } = [];

    public IReadOnlyList<MemoryCapabilityId> AllowedCapabilityIds { get; set; } = [];

    public IReadOnlyList<MemoryCapabilityId> DeniedCapabilityIds { get; set; } = [];

    public IReadOnlyList<MemorySourceScope> AllowedSourceScopes { get; set; } = [];

    public IReadOnlyList<AgentMemoryProviderAssignmentSetting> ProviderAssignments { get; set; } = [];
}

public sealed record AgentMemoryProviderAssignmentSetting(
    MemoryProviderAssignmentScope Scope,
    string Key,
    string ProviderInstanceId);

public static class AgentMemoryAccessMetadata
{
    private const string RootPropertyName = "memory";
    private const string CanUseMemoryToolsPropertyName = "canUseMemoryTools";
    private const string CanUseContextContributionsPropertyName = "canUseContextContributions";
    private const string RequireContextContributionsPropertyName = "requireContextContributions";
    private const string AllowAsyncContextContributionsPropertyName = "allowAsyncContextContributions";
    private const string CanIngestSourcesPropertyName = "canIngestSources";
    private const string PreferredProviderInstanceIdPropertyName = "preferredProviderInstanceId";
    private const string DefaultProviderInstanceIdPropertyName = "defaultProviderInstanceId";
    private const string AllowedProviderInstanceIdsPropertyName = "allowedProviderInstanceIds";
    private const string AllowedCapabilityIdsPropertyName = "allowedCapabilityIds";
    private const string DeniedCapabilityIdsPropertyName = "deniedCapabilityIds";
    private const string AllowedSourceScopesPropertyName = "allowedSourceScopes";
    private const string ProviderAssignmentsPropertyName = "providerAssignments";
    private const string AssignmentScopePropertyName = "scope";
    private const string AssignmentKeyPropertyName = "key";
    private const string AssignmentProviderInstanceIdPropertyName = "providerInstanceId";

    public static AgentMemoryAccessSettings Read(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return new AgentMemoryAccessSettings();
        }

        try
        {
            var root = JsonNode.Parse(configurationJson)?.AsObject();
            var memory = root?[RootPropertyName]?.AsObject();
            if (memory is null)
            {
                return new AgentMemoryAccessSettings();
            }

            return Normalize(new AgentMemoryAccessSettings
            {
                CanUseMemoryTools = TryReadBoolean(memory, CanUseMemoryToolsPropertyName),
                CanUseContextContributions = TryReadBoolean(memory, CanUseContextContributionsPropertyName),
                RequireContextContributions = TryReadBoolean(memory, RequireContextContributionsPropertyName),
                AllowAsyncContextContributions = TryReadBoolean(memory, AllowAsyncContextContributionsPropertyName),
                CanIngestSources = TryReadBoolean(memory, CanIngestSourcesPropertyName),
                PreferredProviderInstanceId = TryReadString(memory, PreferredProviderInstanceIdPropertyName),
                DefaultProviderInstanceId = TryReadString(memory, DefaultProviderInstanceIdPropertyName),
                AllowedProviderInstanceIds = TryReadStringArray(memory, AllowedProviderInstanceIdsPropertyName),
                AllowedCapabilityIds = TryReadCapabilityIds(memory, AllowedCapabilityIdsPropertyName),
                DeniedCapabilityIds = TryReadCapabilityIds(memory, DeniedCapabilityIdsPropertyName),
                AllowedSourceScopes = TryReadEnumArray<MemorySourceScope>(memory, AllowedSourceScopesPropertyName),
                ProviderAssignments = TryReadAssignments(memory)
            });
        }
        catch (JsonException)
        {
            return new AgentMemoryAccessSettings();
        }
    }

    public static string Write(
        string? configurationJson,
        AgentMemoryAccessSettings? settings)
    {
        var normalized = Normalize(settings ?? new AgentMemoryAccessSettings());
        var root = ParseObject(configurationJson);

        if (IsDefault(normalized))
        {
            root.Remove(RootPropertyName);
            return root.ToJsonString();
        }

        root[RootPropertyName] = new JsonObject
        {
            [CanUseMemoryToolsPropertyName] = normalized.CanUseMemoryTools,
            [CanUseContextContributionsPropertyName] = normalized.CanUseContextContributions,
            [RequireContextContributionsPropertyName] = normalized.RequireContextContributions,
            [AllowAsyncContextContributionsPropertyName] = normalized.AllowAsyncContextContributions,
            [CanIngestSourcesPropertyName] = normalized.CanIngestSources,
            [PreferredProviderInstanceIdPropertyName] = normalized.PreferredProviderInstanceId,
            [DefaultProviderInstanceIdPropertyName] = normalized.DefaultProviderInstanceId,
            [AllowedProviderInstanceIdsPropertyName] = ToJsonArray(normalized.AllowedProviderInstanceIds),
            [AllowedCapabilityIdsPropertyName] = ToJsonArray(normalized.AllowedCapabilityIds.Select(capability => capability.Value)),
            [DeniedCapabilityIdsPropertyName] = ToJsonArray(normalized.DeniedCapabilityIds.Select(capability => capability.Value)),
            [AllowedSourceScopesPropertyName] = ToJsonArray(normalized.AllowedSourceScopes.Select(scope => scope.ToString())),
            [ProviderAssignmentsPropertyName] = ToJsonArray(normalized.ProviderAssignments)
        };

        return root.ToJsonString();
    }

    public static AgentMemoryAccessSettings Normalize(AgentMemoryAccessSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        if (!settings.CanUseMemoryTools &&
            !settings.CanUseContextContributions)
        {
            return new AgentMemoryAccessSettings();
        }

        return new AgentMemoryAccessSettings
        {
            CanUseMemoryTools = settings.CanUseMemoryTools,
            CanUseContextContributions = settings.CanUseContextContributions,
            RequireContextContributions = settings.CanUseContextContributions && settings.RequireContextContributions,
            AllowAsyncContextContributions = settings.CanUseContextContributions && settings.AllowAsyncContextContributions,
            CanIngestSources = settings.CanUseMemoryTools && settings.CanIngestSources,
            PreferredProviderInstanceId = NormalizeText(settings.PreferredProviderInstanceId),
            DefaultProviderInstanceId = NormalizeText(settings.DefaultProviderInstanceId),
            AllowedProviderInstanceIds = NormalizeTexts(settings.AllowedProviderInstanceIds),
            AllowedCapabilityIds = NormalizeCapabilities(settings.AllowedCapabilityIds),
            DeniedCapabilityIds = NormalizeCapabilities(settings.DeniedCapabilityIds),
            AllowedSourceScopes = settings.AllowedSourceScopes
                .Distinct()
                .ToArray(),
            ProviderAssignments = NormalizeAssignments(settings.ProviderAssignments)
        };
    }

    private static bool IsDefault(AgentMemoryAccessSettings settings)
    {
        return !settings.CanUseMemoryTools &&
               !settings.CanUseContextContributions &&
               !settings.RequireContextContributions &&
               !settings.AllowAsyncContextContributions &&
               !settings.CanIngestSources &&
               string.IsNullOrWhiteSpace(settings.PreferredProviderInstanceId) &&
               string.IsNullOrWhiteSpace(settings.DefaultProviderInstanceId) &&
               settings.AllowedProviderInstanceIds.Count == 0 &&
               settings.AllowedCapabilityIds.Count == 0 &&
               settings.DeniedCapabilityIds.Count == 0 &&
               settings.AllowedSourceScopes.Count == 0 &&
               settings.ProviderAssignments.Count == 0;
    }

    private static bool TryReadBoolean(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<bool>(out var parsedValue) &&
               parsedValue;
    }

    private static string TryReadString(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value &&
               value.TryGetValue<string>(out var parsedValue)
            ? NormalizeText(parsedValue)
            : string.Empty;
    }

    private static IReadOnlyList<string> TryReadStringArray(JsonObject node, string propertyName)
    {
        if (node[propertyName] is not JsonArray array)
        {
            return [];
        }

        return NormalizeTexts(array
            .OfType<JsonValue>()
            .Select(value => value.TryGetValue<string>(out var text) ? text : string.Empty));
    }

    private static IReadOnlyList<MemoryCapabilityId> TryReadCapabilityIds(JsonObject node, string propertyName)
    {
        if (node[propertyName] is not JsonArray array)
        {
            return [];
        }

        var capabilities = new List<MemoryCapabilityId>();
        foreach (var value in array.OfType<JsonValue>())
        {
            if (!value.TryGetValue<string>(out var text) ||
                string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            try
            {
                capabilities.Add(MemoryCapabilityId.Parse(text));
            }
            catch (ArgumentException)
            {
            }
        }

        return NormalizeCapabilities(capabilities);
    }

    private static IReadOnlyList<TEnum> TryReadEnumArray<TEnum>(JsonObject node, string propertyName)
        where TEnum : struct, Enum
    {
        if (node[propertyName] is not JsonArray array)
        {
            return [];
        }

        return array
            .OfType<JsonValue>()
            .Select(value => value.TryGetValue<string>(out var text) &&
                             Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsed)
                ? (TEnum?)parsed
                : null)
            .OfType<TEnum>()
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<AgentMemoryProviderAssignmentSetting> TryReadAssignments(JsonObject node)
    {
        if (node[ProviderAssignmentsPropertyName] is not JsonArray array)
        {
            return [];
        }

        return NormalizeAssignments(array
            .OfType<JsonObject>()
            .Select(item =>
            {
                var scopeText = TryReadString(item, AssignmentScopePropertyName);
                if (!Enum.TryParse<MemoryProviderAssignmentScope>(scopeText, ignoreCase: true, out var scope))
                {
                    return null;
                }

                return new AgentMemoryProviderAssignmentSetting(
                    scope,
                    TryReadString(item, AssignmentKeyPropertyName),
                    TryReadString(item, AssignmentProviderInstanceIdPropertyName));
            })
            .OfType<AgentMemoryProviderAssignmentSetting>());
    }

    private static JsonArray ToJsonArray(IEnumerable<string> values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonArray ToJsonArray(IEnumerable<AgentMemoryProviderAssignmentSetting> assignments)
    {
        var array = new JsonArray();
        foreach (var assignment in assignments)
        {
            array.Add(new JsonObject
            {
                [AssignmentScopePropertyName] = assignment.Scope.ToString(),
                [AssignmentKeyPropertyName] = assignment.Key,
                [AssignmentProviderInstanceIdPropertyName] = assignment.ProviderInstanceId
            });
        }

        return array;
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

    private static IReadOnlyList<string> NormalizeTexts(IEnumerable<string>? values)
    {
        return values?
            .Select(NormalizeText)
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray()
        ?? [];
    }

    private static IReadOnlyList<MemoryCapabilityId> NormalizeCapabilities(IEnumerable<MemoryCapabilityId>? capabilities)
    {
        return capabilities?
            .Where(capability => !string.IsNullOrWhiteSpace(capability.Value))
            .Distinct()
            .OrderBy(capability => capability.Value, StringComparer.Ordinal)
            .ToArray()
        ?? [];
    }

    private static IReadOnlyList<AgentMemoryProviderAssignmentSetting> NormalizeAssignments(
        IEnumerable<AgentMemoryProviderAssignmentSetting>? assignments)
    {
        return assignments?
            .Select(assignment => assignment with
            {
                Key = NormalizeText(assignment.Key),
                ProviderInstanceId = NormalizeText(assignment.ProviderInstanceId)
            })
            .Where(assignment => assignment.Key.Length > 0 && assignment.ProviderInstanceId.Length > 0)
            .Distinct()
            .ToArray()
        ?? [];
    }

    private static string NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
