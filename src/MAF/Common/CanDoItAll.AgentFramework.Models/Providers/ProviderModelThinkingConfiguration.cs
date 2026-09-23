using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

public sealed record ProviderModelThinkingOverride(
    string Model,
    AgentThinkingEffortSupportStatus Status,
    AgentThinkingEffortControlMode ControlMode,
    IReadOnlyList<AgentReasoningEffortLevel> AllowedEfforts,
    AgentReasoningEffortLevel? DefaultEffort = null) {
    public ProviderModelThinkingEffortCapability ToCapability() => new(
        Model, Status, AgentThinkingEffortCapabilitySource.Configured, AllowedEfforts,
        Summary: $"Administrator-configured thinking controls for model '{Model}'.", ControlMode: ControlMode);
}

public static class ProviderModelThinkingConfiguration {
    public const string PropertyName = "modelThinkingEffortOverrides";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };

    public static IReadOnlyList<ProviderModelThinkingOverride> Read(string? configurationJson) {
        var root = Parse(configurationJson);
        var key = FindKey(root);
        if (key is null) {
            return [];
        }
        try {
            var values = root[key]?.Deserialize<List<ProviderModelThinkingOverride?>>(JsonOptions)
                ?? throw new InvalidOperationException("Model thinking overrides must be an array.");
            var models = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            return values.Select(item => {
                var normalized = Normalize(item ?? throw new InvalidOperationException("Model thinking overrides cannot contain null."));
                if (!models.Add(normalized.Model)) {
                    throw new InvalidOperationException($"Duplicate thinking override for model '{normalized.Model}'.");
                }
                return normalized;
            }).ToArray();
        } catch (JsonException exception) {
            throw new InvalidOperationException("Model thinking overrides contain invalid fields or values.", exception);
        }
    }

    public static ProviderModelThinkingOverride? Find(string? configurationJson, string model) =>
        Read(configurationJson).FirstOrDefault(item => string.Equals(item.Model, model.Trim(), StringComparison.OrdinalIgnoreCase));

    public static string Write(string? configurationJson, string model, ProviderModelThinkingOverride? value) {
        ArgumentException.ThrowIfNullOrWhiteSpace(model);
        var values = Read(configurationJson)
            .Where(item => !string.Equals(item.Model, model.Trim(), StringComparison.OrdinalIgnoreCase)).ToList();
        if (value is not null) {
            values.Add(Normalize(value with { Model = model }));
        }
        var root = Parse(configurationJson);
        if (FindKey(root) is { } key) {
            root.Remove(key);
        }
        if (values.Count > 0) {
            root[PropertyName] = JsonSerializer.SerializeToNode(values.OrderBy(item => item.Model, StringComparer.OrdinalIgnoreCase), JsonOptions);
        }
        return root.ToJsonString();
    }

    public static AgentReasoningEffortLevel? ReadDefault(string? configurationJson, string model, bool includeLegacyOllamaThink = false) =>
        Find(configurationJson, model)?.DefaultEffort ??
        AgentThinkingEffortConfiguration.Read(configurationJson, "provider", includeLegacyOllamaThink);

    public static void ValidateForProvider(string? configurationJson, ProviderKind kind, ProviderTransportKind transport, ProviderProfilePurpose purpose) {
        var overrides = Read(configurationJson);
        if (overrides.Count == 0) {
            return;
        }
        if (purpose != ProviderProfilePurpose.Chat ||
            kind is not (ProviderKind.OpenAi or ProviderKind.AzureOpenAi or ProviderKind.Ollama) ||
            transport is not (ProviderTransportKind.ChatCompletions or ProviderTransportKind.Responses)) {
            throw new InvalidOperationException("Model thinking overrides require a supported chat provider and transport.");
        }
        if (kind != ProviderKind.Ollama && overrides.Any(item => item.ControlMode == AgentThinkingEffortControlMode.BooleanToggle)) {
            throw new InvalidOperationException("Boolean thinking controls are only supported by Ollama providers. Use effort levels for OpenAI.");
        }
        foreach (var item in overrides.Where(item => item.Status == AgentThinkingEffortSupportStatus.Supported)) {
            var effort = ReadDefault(configurationJson, item.Model, kind == ProviderKind.Ollama);
            if (effort is { } value && !item.AllowedEfforts.Contains(value)) {
                throw new InvalidOperationException($"Model '{item.Model}' does not allow the provider default. Choose an allowed per-model default.");
            }
        }
    }

    private static ProviderModelThinkingOverride Normalize(ProviderModelThinkingOverride value) {
        if (string.IsNullOrWhiteSpace(value.Model)) {
            throw new InvalidOperationException("A model thinking override requires a model name.");
        }
        if (value.Status is not (AgentThinkingEffortSupportStatus.Supported or AgentThinkingEffortSupportStatus.Unsupported)) {
            throw new InvalidOperationException($"Model '{value.Model}' must explicitly support or not support thinking.");
        }
        if (value.AllowedEfforts is null || value.AllowedEfforts.Distinct().Count() != value.AllowedEfforts.Count) {
            throw new InvalidOperationException($"Model '{value.Model}' requires distinct allowed thinking efforts.");
        }
        if (value.Status == AgentThinkingEffortSupportStatus.Supported && value.ControlMode == AgentThinkingEffortControlMode.Unspecified ||
            value.Status == AgentThinkingEffortSupportStatus.Unsupported && value.ControlMode != AgentThinkingEffortControlMode.Unspecified) {
            throw new InvalidOperationException($"Model '{value.Model}' thinking control mode must agree with its support status.");
        }
        var capability = AgentThinkingEffortPolicy.NormalizeCapability(value.ToCapability());
        if (value.DefaultEffort is { } effort && !capability.AllowedEfforts.Contains(effort)) {
            throw new InvalidOperationException($"Model '{value.Model}' default thinking effort must be one of its allowed efforts.");
        }
        return value with { Model = capability.Model, AllowedEfforts = capability.AllowedEfforts };
    }

    private static JsonObject Parse(string? configurationJson) {
        if (string.IsNullOrWhiteSpace(configurationJson)) {
            return new JsonObject();
        }
        try {
            return JsonNode.Parse(configurationJson) as JsonObject
                ?? throw new InvalidOperationException("Provider configuration must be a JSON object.");
        } catch (JsonException exception) {
            throw new InvalidOperationException("Provider configuration is not valid JSON.", exception);
        }
    }

    private static string? FindKey(JsonObject root) {
        var keys = root.Select(item => item.Key).Where(key => string.Equals(key, PropertyName, StringComparison.OrdinalIgnoreCase)).ToArray();
        return keys.Length switch {
            0 => null,
            1 => keys[0],
            _ => throw new InvalidOperationException("Provider configuration contains duplicate model thinking override properties.")
        };
    }
}
