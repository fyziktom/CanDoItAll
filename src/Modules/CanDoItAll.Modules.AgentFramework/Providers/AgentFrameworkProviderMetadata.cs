using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace;

namespace CanDoItAll.Modules.AgentFramework;

using AgentFrameworkProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind;
using AgentFrameworkProviderProfile = CanDoItAll.AgentFramework.Models.ProviderProfile;
using AgentFrameworkProviderProfileEditorModel = CanDoItAll.AgentFramework.Models.ProviderProfileEditorModel;
using WorkspaceProviderProfile = CanDoItAll.Modules.Workspace.ProviderProfile;

internal static class AgentFrameworkProviderMetadata
{
    private const string ConnectorPluginKeyPropertyName = "connectorPluginKey";
    private const string ConfigSchemaVersionPropertyName = "configSchemaVersion";
    private const string SecretRecordIdPropertyName =
        ProviderProfileMetadataPropertyNames.SecretRecordId;
    private const string SecretReferencePrefix = "secret:";
    private const string TimeoutSecondsPropertyName = "timeoutSeconds";
    private const string TransportPropertyName = "providerTransport";
    private const string PurposePropertyName = "providerPurpose";
    private const string ProviderKindPropertyName =
        ProviderProfileMetadataPropertyNames.ProviderKind;
    private const string TagsPropertyName = "tags";
    private const string SupportsVisionPropertyName = "supportsVision";
    private const string ThinkingEffortCapabilitiesPropertyName =
        ProviderProfileMetadataPropertyNames.ModelThinkingEffortCapabilities;

    private static readonly IReadOnlyList<string> CanonicalMetadataPropertyNames =
    [
        ConnectorPluginKeyPropertyName,
        ConfigSchemaVersionPropertyName,
        SecretRecordIdPropertyName,
        TimeoutSecondsPropertyName,
        ProviderKindPropertyName,
        TransportPropertyName,
        PurposePropertyName,
        TagsPropertyName,
        SupportsVisionPropertyName,
        ThinkingEffortCapabilitiesPropertyName
    ];

    private static readonly JsonSerializerOptions ThinkingEffortJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
        }
    };

    public static string BuildConfigurationJson(
        WorkspaceProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var configuration = ParseObject(provider.ExtraSettingsJson);
        configuration[ConnectorPluginKeyPropertyName] = provider.ConnectorPluginKey;
        configuration[ConfigSchemaVersionPropertyName] = provider.ConfigSchemaVersion;
        configuration[TimeoutSecondsPropertyName] = provider.TimeoutSeconds;
        if (TryResolveTransport(provider.ExtraSettingsJson, out var transport))
        {
            configuration[TransportPropertyName] = transport.ToString();
        }
        if (TryResolvePurpose(provider.ExtraSettingsJson, out var purpose))
        {
            configuration[PurposePropertyName] = purpose.ToString();
        }
        configuration.Remove(SecretRecordIdPropertyName);

        if (provider.SupportsVision)
        {
            configuration[SupportsVisionPropertyName] = true;
        }
        else
        {
            configuration.Remove(SupportsVisionPropertyName);
        }

        return configuration.ToJsonString();
    }

    public static string BuildExtraSettingsJson(
        string? configurationJson,
        string connectorPluginKey,
        string configSchemaVersion,
        Guid? secretRecordId,
        int timeoutSeconds,
        AgentFrameworkProviderKind providerKind,
        ProviderTransportKind transport,
        ProviderProfilePurpose purpose,
        IReadOnlyList<ProviderModelThinkingEffortCapability> thinkingEffortCapabilities,
        IEnumerable<string>? tags = null)
    {
        var configuration = ParseObject(configurationJson);
        configuration[ConnectorPluginKeyPropertyName] = connectorPluginKey;
        configuration[ConfigSchemaVersionPropertyName] = configSchemaVersion;
        configuration[TimeoutSecondsPropertyName] = timeoutSeconds;
        configuration[ProviderKindPropertyName] = providerKind.ToString();
        configuration[TransportPropertyName] = transport.ToString();
        configuration[PurposePropertyName] = purpose.ToString();
        WriteThinkingEffortCapabilities(configuration, thinkingEffortCapabilities);
        WriteTags(configuration, tags);
        if (secretRecordId.HasValue)
        {
            configuration[SecretRecordIdPropertyName] = secretRecordId.Value.ToString("D");
        }
        else
        {
            configuration.Remove(SecretRecordIdPropertyName);
        }

        return configuration.ToJsonString();
    }

    public static IReadOnlyList<string> ReadTags(
        WorkspaceProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        return ReadTags(provider.ExtraSettingsJson);
    }

    public static IReadOnlyList<string> ReadTags(
        string? configurationJson)
    {
        var configuration = ParseObject(configurationJson);
        if (configuration[TagsPropertyName] is not JsonArray tagsArray)
        {
            return [];
        }

        return tagsArray
            .Select(item => item?.GetValue<string>())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item!.Trim().TrimStart('#').ToLowerInvariant())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static ProviderTransportKind ResolveTransport(
        WorkspaceProviderProfile provider,
        ProviderTransportKind fallback)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return TryResolveTransport(provider.ExtraSettingsJson, out var configuredTransport)
            ? configuredTransport
            : fallback;
    }

    public static AgentFrameworkProviderKind ResolveProviderKind(
        WorkspaceProviderProfile provider,
        AgentFrameworkProviderKind fallback)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var configuration = ParseObject(provider.ExtraSettingsJson);
        if (configuration[ProviderKindPropertyName] is null)
        {
            return fallback;
        }

        if (configuration[ProviderKindPropertyName] is JsonValue value &&
            value.TryGetValue<string>(out var configuredKind) &&
            Enum.TryParse<AgentFrameworkProviderKind>(
                configuredKind,
                ignoreCase: true,
                out var providerKind) &&
            Enum.IsDefined(providerKind))
        {
            return providerKind;
        }

        throw new InvalidOperationException(
            $"Provider configuration property '{ProviderKindPropertyName}' must identify a supported provider kind.");
    }

    public static IReadOnlyList<ProviderModelThinkingEffortCapability>
        ReadThinkingEffortCapabilities(
        string? configurationJson)
    {
        var configuration = ParseObjectStrict(configurationJson);
        var propertyName = FindPropertyName(
            configuration,
            ThinkingEffortCapabilitiesPropertyName);
        if (propertyName is null)
        {
            return [];
        }

        var capabilityMetadata = configuration[propertyName]
            ?? throw new InvalidOperationException(
                $"Provider configuration property '{ThinkingEffortCapabilitiesPropertyName}' must contain thinking-effort capability metadata.");
        try
        {
            var capabilities = capabilityMetadata
                .Deserialize<List<ProviderModelThinkingEffortCapability?>>(
                    ThinkingEffortJsonOptions) ?? [];
            if (capabilities.Any(static capability => capability is null))
            {
                throw new InvalidOperationException(
                    $"Provider configuration property '{ThinkingEffortCapabilitiesPropertyName}' cannot contain null capabilities.");
            }

            return capabilities
                .Select(static capability =>
                    AgentThinkingEffortPolicy.NormalizeCapability(capability!))
                .ToList();
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"Provider configuration property '{ThinkingEffortCapabilitiesPropertyName}' is not valid thinking-effort capability metadata.",
                exception);
        }
    }

    public static bool HasThinkingEffortCapabilities(
        string? configurationJson)
    {
        return FindPropertyName(
            ParseObjectStrict(configurationJson),
            ThinkingEffortCapabilitiesPropertyName) is not null;
    }

    public static string WriteThinkingEffortCapabilities(
        string? configurationJson,
        IReadOnlyList<ProviderModelThinkingEffortCapability> capabilities)
    {
        var configuration = ParseObjectStrict(configurationJson);
        WriteThinkingEffortCapabilities(configuration, capabilities);
        return configuration.ToJsonString();
    }

    public static ProviderProfilePurpose ResolvePurpose(
        WorkspaceProviderProfile provider,
        ProviderProfilePurpose fallback)
    {
        ArgumentNullException.ThrowIfNull(provider);

        return TryResolvePurpose(provider.ExtraSettingsJson, out var configuredPurpose)
            ? configuredPurpose
            : fallback;
    }

    public static Guid? ResolveSecretRecordId(
        AgentFrameworkProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var configuration = ParseObject(provider.ConfigurationJson);
        var configuredSecretRecordId = ReadConfiguredSecretRecordId(configuration);
        var inlineSecretRecordId = ReadInlineSecretRecordId(
            provider.ApiKeyEnvironmentVariable,
            rejectEnvironmentVariableName: false);
        EnsureCompatibleSecretRecordIds(
            configuredSecretRecordId,
            inlineSecretRecordId);
        return inlineSecretRecordId ?? configuredSecretRecordId;
    }

    public static Guid? ResolveSecretRecordId(
        AgentFrameworkProviderProfileEditorModel model)
    {
        ArgumentNullException.ThrowIfNull(model);

        var configuration = ParseObjectStrict(model.ConfigurationJson);
        var configuredSecretRecordId = ReadConfiguredSecretRecordId(configuration);
        var inlineSecretRecordId = ReadInlineSecretRecordId(
            model.ApiKeyEnvironmentVariable,
            rejectEnvironmentVariableName: true);
        EnsureCompatibleSecretRecordIds(
            configuredSecretRecordId,
            inlineSecretRecordId);
        return inlineSecretRecordId ?? configuredSecretRecordId;
    }

    public static string CreateSecretReference(Guid secretRecordId)
    {
        if (secretRecordId == Guid.Empty)
        {
            throw new ArgumentException(
                "A secret reference requires a non-empty record id.",
                nameof(secretRecordId));
        }

        return $"{SecretReferencePrefix}{secretRecordId:D}";
    }

    public static string ResolveConnectorPluginKey(
        AgentFrameworkProviderProfileEditorModel model,
        WorkspaceProviderProfile? current)
    {
        ArgumentNullException.ThrowIfNull(model);

        var configuration = ParseObjectStrict(model.ConfigurationJson);
        if (configuration.TryGetPropertyValue(
                ConnectorPluginKeyPropertyName,
                out var configuredPlugin))
        {
            if (configuredPlugin is JsonValue value &&
                value.TryGetValue<string>(out var configuredPluginKey) &&
                !string.IsNullOrWhiteSpace(configuredPluginKey))
            {
                var normalizedPluginKey = configuredPluginKey.Trim();
                if (current is null ||
                    !string.Equals(
                        normalizedPluginKey,
                        current.ConnectorPluginKey,
                        StringComparison.Ordinal) ||
                    IsConnectorCompatibleWithEditor(
                        normalizedPluginKey,
                        model))
                {
                    return normalizedPluginKey;
                }
            }
            else
            {
                throw new InvalidOperationException(
                    $"Provider configuration property '{ConnectorPluginKeyPropertyName}' must identify a connector plugin.");
            }
        }

        if (!string.IsNullOrWhiteSpace(current?.ConnectorPluginKey) &&
            IsConnectorCompatibleWithEditor(
                current.ConnectorPluginKey,
                model))
        {
            return current.ConnectorPluginKey;
        }

        if (string.Equals(model.BaseUrl, ScenarioHarnessProviderAdapter.BaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return ScenarioHarnessProviderAdapter.PluginKey;
        }

        if (string.Equals(model.BaseUrl, ProcessMockProviderAdapter.BaseUrl, StringComparison.OrdinalIgnoreCase))
        {
            return ProcessMockProviderAdapter.PluginKey;
        }

        return model.Kind switch
        {
            AgentFrameworkProviderKind.ComfyUi => ComfyUiProviderAdapter.PluginKey,
            AgentFrameworkProviderKind.Ollama when LooksLikeLocalOllama(model.BaseUrl) => OllamaProviderAdapter.PluginKey,
            AgentFrameworkProviderKind.Ollama => OllamaRemoteProviderAdapter.PluginKey,
            AgentFrameworkProviderKind.OpenAi or AgentFrameworkProviderKind.AzureOpenAi => OpenAiProviderAdapter.PluginKey,
            _ => throw new InvalidOperationException($"No workspace connector plugin mapping exists for provider kind '{model.Kind}'.")
        };
    }

    public static string ResolveConfigSchemaVersion(
        AgentFrameworkProviderProfileEditorModel model,
        WorkspaceProviderProfile? current,
        string defaultVersion)
    {
        ArgumentNullException.ThrowIfNull(model);

        var configuration = ParseObjectStrict(model.ConfigurationJson);
        if (configuration.TryGetPropertyValue(
                ConfigSchemaVersionPropertyName,
                out var configuredSchemaVersion))
        {
            if (configuredSchemaVersion is JsonValue value &&
                value.TryGetValue<string>(out var configuredVersion) &&
                !string.IsNullOrWhiteSpace(configuredVersion))
            {
                return configuredVersion.Trim();
            }

            throw new InvalidOperationException(
                $"Provider configuration property '{ConfigSchemaVersionPropertyName}' must identify a schema version.");
        }

        if (!string.IsNullOrWhiteSpace(current?.ConfigSchemaVersion))
        {
            return current.ConfigSchemaVersion;
        }

        return defaultVersion;
    }

    public static int ResolveTimeoutSeconds(
        AgentFrameworkProviderProfileEditorModel model,
        int fallbackValue)
    {
        ArgumentNullException.ThrowIfNull(model);

        var configuration = ParseObjectStrict(model.ConfigurationJson);
        if (configuration.TryGetPropertyValue(
                TimeoutSecondsPropertyName,
                out var configuredTimeout))
        {
            if (configuredTimeout is JsonValue value &&
                value.TryGetValue<int>(out var timeoutSeconds) &&
                timeoutSeconds >= 5)
            {
                return timeoutSeconds;
            }

            throw new InvalidOperationException(
                $"Provider configuration property '{TimeoutSecondsPropertyName}' must be an integer of at least 5 seconds.");
        }

        return Math.Max(5, fallbackValue);
    }

    private static JsonObject ParseObject(
        string? json)
    {
        return ParseObjectStrict(json);
    }

    private static JsonObject ParseObjectStrict(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new JsonObject();
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    "Provider configuration must be a JSON object.");
            }

            RejectDuplicateMetadataPropertyAliases(document.RootElement);
            var configuration = JsonNode.Parse(json) as JsonObject
                ?? throw new InvalidOperationException(
                    "Provider configuration must be a JSON object.");
            CanonicalizeMetadataPropertyAliases(configuration);
            return configuration;
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                "Provider configuration is not valid JSON.",
                exception);
        }
    }

    private static void RejectDuplicateMetadataPropertyAliases(
        JsonElement configuration)
    {
        var seenProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in configuration.EnumerateObject())
        {
            var canonicalName = ResolveCanonicalMetadataPropertyName(property.Name);
            if (canonicalName is null || seenProperties.Add(canonicalName))
            {
                continue;
            }

            throw new InvalidOperationException(
                $"Provider configuration property '{canonicalName}' cannot be defined more than once with case-insensitive aliases.");
        }
    }

    private static void CanonicalizeMetadataPropertyAliases(
        JsonObject configuration)
    {
        foreach (var canonicalName in CanonicalMetadataPropertyNames)
        {
            var aliases = configuration
                .Select(property => property.Key)
                .Where(propertyName => string.Equals(
                    propertyName,
                    canonicalName,
                    StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (aliases.Count == 0)
            {
                continue;
            }

            var selectedAlias = aliases.FirstOrDefault(alias => string.Equals(
                alias,
                canonicalName,
                StringComparison.Ordinal)) ?? aliases[0];
            var value = configuration[selectedAlias];
            foreach (var alias in aliases)
            {
                configuration.Remove(alias);
            }

            configuration[canonicalName] = value;
        }
    }

    private static string? ResolveCanonicalMetadataPropertyName(
        string propertyName)
    {
        return CanonicalMetadataPropertyNames.FirstOrDefault(candidate =>
            string.Equals(
                candidate,
                propertyName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static Guid? ReadConfiguredSecretRecordId(
        JsonObject configuration)
    {
        if (!configuration.TryGetPropertyValue(
                SecretRecordIdPropertyName,
                out var configuredSecretRecord))
        {
            return null;
        }

        if (configuredSecretRecord is JsonValue value &&
            value.TryGetValue<string>(out var secretValue) &&
            Guid.TryParse(secretValue, out var secretRecordId) &&
            secretRecordId != Guid.Empty)
        {
            return secretRecordId;
        }

        throw new InvalidOperationException(
            $"Provider configuration property '{SecretRecordIdPropertyName}' must identify a non-empty secret record id.");
    }

    private static Guid? ReadInlineSecretRecordId(
        string? secretReference,
        bool rejectEnvironmentVariableName)
    {
        var normalizedReference = secretReference?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(normalizedReference))
        {
            return null;
        }

        if (!normalizedReference.StartsWith(
                SecretReferencePrefix,
                StringComparison.OrdinalIgnoreCase))
        {
            if (!rejectEnvironmentVariableName)
            {
                return null;
            }

            throw new InvalidOperationException(
                "Provider credential bindings must use 'secret:<non-empty-record-id>'.");
        }

        if (Guid.TryParse(
                normalizedReference[SecretReferencePrefix.Length..],
                out var secretRecordId) &&
            secretRecordId != Guid.Empty)
        {
            return secretRecordId;
        }

        throw new InvalidOperationException(
            "Provider credential bindings must use 'secret:<non-empty-record-id>'.");
    }

    private static void EnsureCompatibleSecretRecordIds(
        Guid? configuredSecretRecordId,
        Guid? inlineSecretRecordId)
    {
        if (configuredSecretRecordId.HasValue &&
            inlineSecretRecordId.HasValue &&
            configuredSecretRecordId != inlineSecretRecordId)
        {
            throw new InvalidOperationException(
                "Provider configuration contains conflicting explicit secret record references.");
        }
    }

    private static void WriteTags(
        JsonObject configuration,
        IEnumerable<string>? tags)
    {
        var normalizedTags = tags?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim().TrimStart('#').ToLowerInvariant())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
            .ToArray()
            ?? [];
        if (normalizedTags.Length == 0)
        {
            configuration.Remove(TagsPropertyName);
            return;
        }

        var tagArray = new JsonArray();
        foreach (var tag in normalizedTags)
        {
            tagArray.Add(JsonValue.Create(tag));
        }

        configuration[TagsPropertyName] = tagArray;
    }

    private static void WriteThinkingEffortCapabilities(
        JsonObject configuration,
        IReadOnlyList<ProviderModelThinkingEffortCapability> capabilities)
    {
        foreach (var propertyName in configuration
                     .Select(item => item.Key)
                     .Where(item => string.Equals(
                         item,
                         ThinkingEffortCapabilitiesPropertyName,
                         StringComparison.OrdinalIgnoreCase))
                     .ToList())
        {
            configuration.Remove(propertyName);
        }

        if (capabilities.Count == 0)
        {
            return;
        }

        configuration[ThinkingEffortCapabilitiesPropertyName] =
            JsonSerializer.SerializeToNode(
                capabilities,
                ThinkingEffortJsonOptions);
    }

    private static string? FindPropertyName(
        JsonObject configuration,
        string propertyName)
    {
        return configuration
            .Select(item => item.Key)
            .FirstOrDefault(item => string.Equals(
                item,
                propertyName,
                StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolveTransport(
        string? json,
        out ProviderTransportKind transport)
    {
        transport = default;
        var configuration = ParseObject(json);
        if (configuration[TransportPropertyName] is not JsonValue value ||
            !value.TryGetValue<string>(out var configuredTransport) ||
            string.IsNullOrWhiteSpace(configuredTransport))
        {
            return false;
        }

        return Enum.TryParse(configuredTransport.Trim(), ignoreCase: true, out transport);
    }

    private static bool TryResolvePurpose(
        string? json,
        out ProviderProfilePurpose purpose)
    {
        purpose = default;
        var configuration = ParseObject(json);
        if (configuration[PurposePropertyName] is not JsonValue value ||
            !value.TryGetValue<string>(out var configuredPurpose) ||
            string.IsNullOrWhiteSpace(configuredPurpose))
        {
            return false;
        }

        return Enum.TryParse(configuredPurpose.Trim(), ignoreCase: true, out purpose);
    }

    private static bool LooksLikeLocalOllama(
        string? baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return true;
        }

        return baseUrl.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
               baseUrl.Contains(":11434", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsConnectorCompatibleWithEditor(
        string connectorPluginKey,
        AgentFrameworkProviderProfileEditorModel model)
    {
        return connectorPluginKey switch
        {
            ScenarioHarnessProviderAdapter.PluginKey => string.Equals(
                model.BaseUrl,
                ScenarioHarnessProviderAdapter.BaseUrl,
                StringComparison.OrdinalIgnoreCase),
            ProcessMockProviderAdapter.PluginKey => string.Equals(
                model.BaseUrl,
                ProcessMockProviderAdapter.BaseUrl,
                StringComparison.OrdinalIgnoreCase),
            OpenAiProviderAdapter.PluginKey =>
                model.Kind is AgentFrameworkProviderKind.OpenAi or
                    AgentFrameworkProviderKind.AzureOpenAi,
            OllamaProviderAdapter.PluginKey =>
                model.Kind == AgentFrameworkProviderKind.Ollama &&
                LooksLikeLocalOllama(model.BaseUrl),
            OllamaRemoteProviderAdapter.PluginKey =>
                model.Kind == AgentFrameworkProviderKind.Ollama &&
                !LooksLikeLocalOllama(model.BaseUrl),
            ComfyUiProviderAdapter.PluginKey =>
                model.Kind == AgentFrameworkProviderKind.ComfyUi,
            _ => true
        };
    }
}
