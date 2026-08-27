namespace CanDoItAll.AgentFramework.Models;

public enum AgentThinkingEffortSupportStatus
{
    Supported,
    Unsupported,
    Unknown
}

public enum AgentThinkingEffortCapabilitySource
{
    Defined,
    Discovered
}

public enum AgentThinkingEffortControlMode
{
    Unspecified,
    BooleanToggle,
    EffortLevels
}

public sealed record ProviderModelThinkingEffortCapability(
    string Model,
    AgentThinkingEffortSupportStatus Status,
    AgentThinkingEffortCapabilitySource Source,
    IReadOnlyList<AgentReasoningEffortLevel> AllowedEfforts,
    string ModelFamily = "",
    string Summary = "",
    AgentThinkingEffortControlMode ControlMode = AgentThinkingEffortControlMode.Unspecified);

public static class AgentThinkingEffortPolicy
{
    public const string ModelParametersConfigurationPropertyName = AgentThinkingEffortConfiguration.ModelParametersPropertyName;
    public const string ReasoningEffortConfigurationPropertyName = AgentThinkingEffortConfiguration.ReasoningEffortPropertyName;
    private static readonly IReadOnlyList<AgentReasoningEffortLevel> OllamaBooleanEfforts =
    [
        AgentReasoningEffortLevel.None,
        AgentReasoningEffortLevel.Medium
    ];

    private static readonly IReadOnlyList<AgentReasoningEffortLevel> OllamaLevelEfforts =
    [
        AgentReasoningEffortLevel.Low,
        AgentReasoningEffortLevel.Medium,
        AgentReasoningEffortLevel.High
    ];

    private static readonly string[] KnownOllamaThinkingModelPrefixes =
    [
        "deepseek-r1",
        "gemma4",
        "gpt-oss",
        "gptoss",
        "qwen3"
    ];

    public static ProviderModelThinkingEffortCapability ResolveCapability(
        ProviderProfile provider,
        string? model)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var normalizedModel = NormalizeModel(model);
        if (provider.IsSourceManaged) {
            return CreateUnknownCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Thinking effort for shared model '{provider.GetModelDisplayName(normalizedModel)}' is managed by the source instance. Use Provider default.");
        }
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            return CreateUnknownCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Defined,
                "Select a model to determine whether thinking effort can be overridden.");
        }

        if (provider.Purpose != ProviderProfilePurpose.Chat)
        {
            return CreateUnsupportedCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Provider '{provider.Name}' is not a chat provider.");
        }

        if (provider.Transport is not (ProviderTransportKind.Responses or ProviderTransportKind.ChatCompletions))
        {
            return CreateUnsupportedCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Transport '{provider.Transport}' cannot apply configurable thinking effort.");
        }

        var storedCapability = provider.ModelThinkingEffortCapabilities.FirstOrDefault(item =>
            ModelIdsEqual(item.Model, normalizedModel) &&
            (provider.Kind == ProviderKind.AzureOpenAi ||
             provider.Kind == ProviderKind.Ollama &&
             item.Source == AgentThinkingEffortCapabilitySource.Discovered));
        if (storedCapability is not null)
        {
            var normalizedCapability = NormalizeCapability(storedCapability with { Model = normalizedModel });
            if (provider.Kind == ProviderKind.Ollama &&
                normalizedCapability.Status == AgentThinkingEffortSupportStatus.Unknown)
            {
                var definedCapability = ResolveOllamaCapability(normalizedModel);
                if (definedCapability.Status == AgentThinkingEffortSupportStatus.Supported)
                {
                    return definedCapability;
                }
            }

            return normalizedCapability;
        }

        return provider.Kind switch
        {
            ProviderKind.OpenAi => ResolveOpenAiCapability(
                normalizedModel,
                provider.Transport),
            ProviderKind.AzureOpenAi => CreateUnknownCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Azure OpenAI deployment '{normalizedModel}' has no provider-scoped thinking-effort capability metadata."),
            ProviderKind.Ollama => ResolveOllamaCapability(normalizedModel),
            _ => CreateUnsupportedCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Provider kind '{provider.Kind}' does not support configurable thinking effort.")
        };
    }

    public static ProviderModelThinkingEffortCapability ResolveDefinedCapability(
        ProviderKind providerKind,
        ProviderTransportKind providerTransport,
        string? model)
    {
        var normalizedModel = NormalizeModel(model);
        if (string.IsNullOrWhiteSpace(normalizedModel))
        {
            return CreateUnknownCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Defined,
                "Select a model to determine whether thinking effort can be overridden.");
        }

        if (providerTransport is not (ProviderTransportKind.Responses or ProviderTransportKind.ChatCompletions))
        {
            return CreateUnsupportedCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Transport '{providerTransport}' cannot apply configurable thinking effort.");
        }

        return providerKind switch
        {
            ProviderKind.OpenAi => ResolveOpenAiCapability(
                normalizedModel,
                providerTransport),
            ProviderKind.AzureOpenAi => CreateUnknownCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Azure OpenAI deployment '{normalizedModel}' requires provider-scoped thinking-effort capability metadata."),
            ProviderKind.Ollama => ResolveOllamaCapability(normalizedModel),
            _ => CreateUnsupportedCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Provider kind '{providerKind}' does not support configurable thinking effort.")
        };
    }

    public static ProviderModelThinkingEffortCapability CreateDiscoveredCapability(
        string model,
        string? modelFamily,
        AgentThinkingEffortSupportStatus status)
    {
        var normalizedModel = NormalizeModel(model);
        var normalizedFamily = modelFamily?.Trim() ?? string.Empty;
        return status switch
        {
            AgentThinkingEffortSupportStatus.Supported => new ProviderModelThinkingEffortCapability(
                normalizedModel,
                status,
                AgentThinkingEffortCapabilitySource.Discovered,
                ResolveOllamaEfforts(normalizedModel, normalizedFamily),
                normalizedFamily,
                CreateOllamaSupportedSummary(normalizedModel, normalizedFamily),
                ResolveOllamaControlMode(normalizedModel, normalizedFamily)),
            AgentThinkingEffortSupportStatus.Unsupported => CreateUnsupportedCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Discovered,
                $"Model '{normalizedModel}' does not advertise configurable thinking.",
                normalizedFamily),
            AgentThinkingEffortSupportStatus.Unknown => CreateUnknownCapability(
                normalizedModel,
                AgentThinkingEffortCapabilitySource.Discovered,
                $"Model '{normalizedModel}' did not report thinking capability metadata.",
                normalizedFamily),
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported thinking-effort support status.")
        };
    }

    public static ProviderModelThinkingEffortCapability NormalizeCapability(
        ProviderModelThinkingEffortCapability capability)
    {
        ArgumentNullException.ThrowIfNull(capability);

        if (!Enum.IsDefined(capability.Status))
        {
            throw new InvalidOperationException(
                $"Model '{capability.Model}' has an invalid thinking-effort support status.");
        }

        if (!Enum.IsDefined(capability.Source))
        {
            throw new InvalidOperationException(
                $"Model '{capability.Model}' has an invalid thinking-effort capability source.");
        }

        if (!Enum.IsDefined(capability.ControlMode))
        {
            throw new InvalidOperationException(
                $"Model '{capability.Model}' has an invalid thinking-effort control mode.");
        }

        var configuredEfforts = (capability.AllowedEfforts ?? []).ToList();
        if (configuredEfforts.Any(effort => !Enum.IsDefined(effort)))
        {
            throw new InvalidOperationException(
                $"Model '{capability.Model}' defines an invalid thinking-effort value.");
        }

        var allowedEfforts = configuredEfforts
            .Distinct()
            .OrderBy(GetEffortOrder)
            .ToList();
        if (capability.Status == AgentThinkingEffortSupportStatus.Supported && allowedEfforts.Count == 0)
        {
            throw new InvalidOperationException(
                $"Model '{capability.Model}' is marked as thinking-capable but defines no allowed efforts.");
        }

        if (capability.Status != AgentThinkingEffortSupportStatus.Supported && allowedEfforts.Count > 0)
        {
            throw new InvalidOperationException(
                $"Model '{capability.Model}' defines thinking efforts while its support status is '{capability.Status}'.");
        }

        var controlMode = capability.Status == AgentThinkingEffortSupportStatus.Supported
            ? ResolveNormalizedControlMode(capability.ControlMode, allowedEfforts)
            : AgentThinkingEffortControlMode.Unspecified;
        if (controlMode == AgentThinkingEffortControlMode.BooleanToggle &&
            (allowedEfforts.Count != 2 ||
             !allowedEfforts.Contains(AgentReasoningEffortLevel.None) ||
             !allowedEfforts.Contains(AgentReasoningEffortLevel.Medium)))
        {
            throw new InvalidOperationException(
                $"Model '{capability.Model}' uses BooleanToggle thinking control but must define exactly None and Medium efforts.");
        }

        return capability with
        {
            Model = NormalizeModel(capability.Model),
            AllowedEfforts = allowedEfforts,
            ModelFamily = capability.ModelFamily?.Trim() ?? string.Empty,
            Summary = capability.Summary?.Trim() ?? string.Empty,
            ControlMode = controlMode
        };
    }

    public static AgentReasoningEffortLevel? ResolveEffectiveEffort(
        ProviderProfile provider,
        string model,
        string? agentConfigurationJson)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return ResolveEffectiveEffort(
            provider.Name,
            model,
            ResolveCapability(provider, model),
            provider.ConfigurationJson,
            agentConfigurationJson,
            includeLegacyOllamaThink: provider.Kind == ProviderKind.Ollama);
    }

    public static AgentReasoningEffortLevel? ResolveDefinedEffectiveEffort(
        ProviderKind providerKind,
        ProviderTransportKind providerTransport,
        string model,
        string? providerConfigurationJson,
        string? agentConfigurationJson)
    {
        return ResolveEffectiveEffort(
            providerKind.ToString(),
            model,
            ResolveDefinedCapability(providerKind, providerTransport, model),
            providerConfigurationJson,
            agentConfigurationJson,
            includeLegacyOllamaThink: providerKind == ProviderKind.Ollama);
    }

    public static AgentReasoningEffortLevel? ResolveProviderDefault(
        ProviderProfile provider,
        string model)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var capability = ResolveCapability(provider, model);
        if (capability.Status != AgentThinkingEffortSupportStatus.Supported)
        {
            return null;
        }

        var providerDefault = ReadConfiguredEffort(
            provider.ConfigurationJson,
            "provider",
            includeLegacyOllamaThink: provider.Kind == ProviderKind.Ollama);
        if (providerDefault is null)
        {
            return null;
        }

        EnsureOverrideSupported(provider.Name, model, capability, providerDefault.Value, "provider");
        return providerDefault;
    }

    public static void EnsureOverrideSupported(
        ProviderProfile provider,
        string model,
        AgentReasoningEffortLevel effort,
        string configurationOwner = "agent")
    {
        ArgumentNullException.ThrowIfNull(provider);
        EnsureOverrideSupported(
            provider.Name,
            model,
            ResolveCapability(provider, model),
            effort,
            configurationOwner);
    }

    public static bool IsOverrideSupported(
        ProviderProfile provider,
        string model,
        AgentReasoningEffortLevel effort)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var capability = ResolveCapability(provider, model);
        return capability.Status == AgentThinkingEffortSupportStatus.Supported &&
               capability.AllowedEfforts.Contains(effort);
    }

    public static AgentReasoningEffortLevel? ReadConfiguredEffort(
        string? configurationJson,
        string configurationOwner)
    {
        return AgentThinkingEffortConfiguration.Read(configurationJson, configurationOwner);
    }

    internal static AgentReasoningEffortLevel? ReadConfiguredEffort(
        string? configurationJson,
        string configurationOwner,
        bool includeLegacyOllamaThink)
    {
        return AgentThinkingEffortConfiguration.Read(
            configurationJson,
            configurationOwner,
            includeLegacyOllamaThink);
    }

    public static string WriteAgentOverride(
        string? configurationJson,
        AgentReasoningEffortLevel? effort)
    {
        return AgentThinkingEffortConfiguration.WriteAgentOverride(configurationJson, effort);
    }

    public static string WriteProviderDefault(
        string? configurationJson,
        AgentReasoningEffortLevel? effort)
    {
        return AgentThinkingEffortConfiguration.WriteProviderDefault(configurationJson, effort);
    }

    public static bool IsDefinedOpenAiReasoningModel(string? model)
    {
        return OpenAiThinkingEffortModelRegistry.Find(model)?.Status ==
               AgentThinkingEffortSupportStatus.Supported;
    }

    public static string FormatEffort(AgentReasoningEffortLevel effort)
    {
        return AgentThinkingEffortConfiguration.Format(effort);
    }

    private static ProviderModelThinkingEffortCapability ResolveOpenAiCapability(
        string model,
        ProviderTransportKind transport)
    {
        var definition = OpenAiThinkingEffortModelRegistry.Find(model);
        if (definition is null)
        {
            return CreateUnknownCapability(
                model,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Thinking-effort capability is not defined for model '{model}'.");
        }

        if (definition.Status == AgentThinkingEffortSupportStatus.Unsupported)
        {
            return CreateUnsupportedCapability(
                model,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Model '{model}' does not support configurable thinking effort.");
        }

        if (!definition.AllowedTransports.Contains(transport))
        {
            var allowedTransports = string.Join(
                " or ",
                definition.AllowedTransports.Select(item => $"{item} transport"));
            return CreateUnsupportedCapability(
                model,
                AgentThinkingEffortCapabilitySource.Defined,
                $"Model '{model}' cannot apply configurable thinking effort with the {transport} transport. Use the {allowedTransports}.");
        }

        return new ProviderModelThinkingEffortCapability(
            model,
            AgentThinkingEffortSupportStatus.Supported,
            AgentThinkingEffortCapabilitySource.Defined,
            definition.AllowedEfforts,
            Summary: $"Model '{model}' supports configurable thinking effort.",
            ControlMode: AgentThinkingEffortControlMode.EffortLevels);
    }

    private static ProviderModelThinkingEffortCapability ResolveOllamaCapability(string model)
    {
        var normalizedModel = model.ToLowerInvariant();
        if (IsOllamaGptOssModel(normalizedModel, string.Empty) ||
            KnownOllamaThinkingModelPrefixes.Any(prefix => MatchesModelPrefix(normalizedModel, prefix)))
        {
            var modelFamily = ResolveKnownOllamaModelFamily(model);
            return new ProviderModelThinkingEffortCapability(
                model,
                AgentThinkingEffortSupportStatus.Supported,
                AgentThinkingEffortCapabilitySource.Defined,
                ResolveOllamaEfforts(model, modelFamily),
                ModelFamily: modelFamily,
                Summary: CreateOllamaSupportedSummary(model, modelFamily),
                ControlMode: ResolveOllamaControlMode(model, modelFamily));
        }

        return CreateUnknownCapability(
            model,
            AgentThinkingEffortCapabilitySource.Defined,
            $"Thinking-effort capability has not been discovered for Ollama model '{model}'. Test the provider to refresh model capabilities.");
    }

    private static AgentReasoningEffortLevel? ResolveEffectiveEffort(
        string providerName,
        string model,
        ProviderModelThinkingEffortCapability capability,
        string? providerConfigurationJson,
        string? agentConfigurationJson,
        bool includeLegacyOllamaThink)
    {
        var agentOverride = ReadConfiguredEffort(
            agentConfigurationJson,
            "agent",
            includeLegacyOllamaThink);
        if (agentOverride is not null)
        {
            EnsureOverrideSupported(providerName, model, capability, agentOverride.Value, "agent");
            return agentOverride;
        }

        if (capability.Status != AgentThinkingEffortSupportStatus.Supported)
        {
            return null;
        }

        var providerDefault = ReadConfiguredEffort(
            providerConfigurationJson,
            "provider",
            includeLegacyOllamaThink);
        if (providerDefault is null)
        {
            return null;
        }

        EnsureOverrideSupported(providerName, model, capability, providerDefault.Value, "provider");
        return providerDefault;
    }

    private static void EnsureOverrideSupported(
        string providerName,
        string model,
        ProviderModelThinkingEffortCapability capability,
        AgentReasoningEffortLevel effort,
        string configurationOwner)
    {
        if (capability.Status != AgentThinkingEffortSupportStatus.Supported)
        {
            throw new InvalidOperationException(
                $"The {configurationOwner} thinking-effort override '{FormatEffort(effort)}' cannot be applied to provider '{providerName}' model '{model}': {capability.Summary}");
        }

        if (!capability.AllowedEfforts.Contains(effort))
        {
            var allowedValues = string.Join(", ", capability.AllowedEfforts.Select(FormatEffort));
            throw new InvalidOperationException(
                $"The {configurationOwner} thinking-effort override '{FormatEffort(effort)}' is not supported by provider '{providerName}' model '{model}'. Allowed values are {allowedValues}.");
        }
    }

    private static ProviderModelThinkingEffortCapability CreateUnsupportedCapability(
        string model,
        AgentThinkingEffortCapabilitySource source,
        string summary,
        string modelFamily = "")
    {
        return new ProviderModelThinkingEffortCapability(
            model,
            AgentThinkingEffortSupportStatus.Unsupported,
            source,
            [],
            modelFamily,
            summary);
    }

    private static ProviderModelThinkingEffortCapability CreateUnknownCapability(
        string model,
        AgentThinkingEffortCapabilitySource source,
        string summary,
        string modelFamily = "")
    {
        return new ProviderModelThinkingEffortCapability(
            model,
            AgentThinkingEffortSupportStatus.Unknown,
            source,
            [],
            modelFamily,
            summary);
    }

    private static bool ModelIdsEqual(string? left, string? right)
    {
        return string.Equals(
            NormalizeModelForComparison(left),
            NormalizeModelForComparison(right),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeModelForComparison(string? model)
    {
        var normalizedModel = NormalizeModel(model);
        return normalizedModel.EndsWith(":latest", StringComparison.OrdinalIgnoreCase)
            ? normalizedModel[..^":latest".Length]
            : normalizedModel;
    }

    private static string NormalizeModel(string? model)
    {
        return string.IsNullOrWhiteSpace(model)
            ? string.Empty
            : model.Trim();
    }

    private static bool MatchesModelPrefix(string normalizedModel, string prefix)
    {
        return string.Equals(normalizedModel, prefix, StringComparison.Ordinal) ||
               normalizedModel.StartsWith(prefix + "-", StringComparison.Ordinal) ||
               normalizedModel.StartsWith(prefix + ".", StringComparison.Ordinal) ||
               normalizedModel.StartsWith(prefix + ":", StringComparison.Ordinal);
    }

    private static IReadOnlyList<AgentReasoningEffortLevel> ResolveOllamaEfforts(
        string model,
        string modelFamily)
    {
        return IsOllamaGptOssModel(model, modelFamily)
            ? OllamaLevelEfforts
            : OllamaBooleanEfforts;
    }

    private static AgentThinkingEffortControlMode ResolveOllamaControlMode(
        string model,
        string modelFamily)
    {
        return IsOllamaGptOssModel(model, modelFamily)
            ? AgentThinkingEffortControlMode.EffortLevels
            : AgentThinkingEffortControlMode.BooleanToggle;
    }

    private static AgentThinkingEffortControlMode ResolveNormalizedControlMode(
        AgentThinkingEffortControlMode configuredMode,
        IReadOnlyCollection<AgentReasoningEffortLevel> allowedEfforts)
    {
        if (configuredMode != AgentThinkingEffortControlMode.Unspecified)
        {
            return configuredMode;
        }

        return allowedEfforts.Count == 2 &&
               allowedEfforts.Contains(AgentReasoningEffortLevel.None) &&
               allowedEfforts.Contains(AgentReasoningEffortLevel.Medium)
            ? AgentThinkingEffortControlMode.BooleanToggle
            : AgentThinkingEffortControlMode.EffortLevels;
    }

    private static string CreateOllamaSupportedSummary(string model, string modelFamily)
    {
        return IsOllamaGptOssModel(model, modelFamily)
            ? $"Model '{model}' supports low, medium, and high thinking effort; thinking cannot be disabled."
            : $"Model '{model}' supports enabling or disabling thinking.";
    }

    private static bool IsOllamaGptOssModel(string model, string modelFamily)
    {
        var normalizedModel = NormalizeModel(model).ToLowerInvariant();
        var normalizedFamily = modelFamily.Trim().ToLowerInvariant();
        return normalizedFamily == "gptoss" ||
               normalizedFamily == "gpt-oss" ||
               normalizedModel.StartsWith("gptoss", StringComparison.Ordinal) ||
               MatchesModelPrefix(normalizedModel, "gpt-oss");
    }

    private static string ResolveKnownOllamaModelFamily(string model)
    {
        return IsOllamaGptOssModel(model, string.Empty)
            ? "gptoss"
            : string.Empty;
    }

    private static int GetEffortOrder(AgentReasoningEffortLevel effort)
    {
        return effort switch
        {
            AgentReasoningEffortLevel.None => 0,
            AgentReasoningEffortLevel.Minimal => 1,
            AgentReasoningEffortLevel.Low => 2,
            AgentReasoningEffortLevel.Medium => 3,
            AgentReasoningEffortLevel.High => 4,
            AgentReasoningEffortLevel.ExtraHigh => 5,
            AgentReasoningEffortLevel.Max => 6,
            _ => throw new ArgumentOutOfRangeException(nameof(effort), effort, "Unsupported thinking effort.")
        };
    }
}
