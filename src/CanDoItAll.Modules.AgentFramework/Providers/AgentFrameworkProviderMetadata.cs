using System.Text.Json;
using System.Text.Json.Nodes;
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
    private const string SecretRecordIdPropertyName = "secretRecordId";
    private const string TimeoutSecondsPropertyName = "timeoutSeconds";
    private const string TransportPropertyName = "providerTransport";
    private const string TagsPropertyName = "tags";
    private const string SupportsVisionPropertyName = "supportsVision";

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
        if (provider.ApiKeySecretId.HasValue)
        {
            configuration[SecretRecordIdPropertyName] = provider.ApiKeySecretId.Value.ToString("D");
        }
        else
        {
            configuration.Remove(SecretRecordIdPropertyName);
        }

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
        ProviderTransportKind transport,
        IEnumerable<string>? tags = null)
    {
        var configuration = ParseObject(configurationJson);
        configuration[ConnectorPluginKeyPropertyName] = connectorPluginKey;
        configuration[ConfigSchemaVersionPropertyName] = configSchemaVersion;
        configuration[TimeoutSecondsPropertyName] = timeoutSeconds;
        configuration[TransportPropertyName] = transport.ToString();
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

    public static Guid? ResolveSecretRecordId(
        AgentFrameworkProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var configuration = ParseObject(provider.ConfigurationJson);
        if (configuration[SecretRecordIdPropertyName] is JsonValue value &&
            value.TryGetValue<string>(out var secretValue) &&
            Guid.TryParse(secretValue, out var secretId))
        {
            return secretId;
        }

        const string SecretPrefix = "secret:";
        if (!string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable) &&
            provider.ApiKeyEnvironmentVariable.StartsWith(SecretPrefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(provider.ApiKeyEnvironmentVariable[SecretPrefix.Length..], out var inlineSecretId))
        {
            return inlineSecretId;
        }

        return null;
    }

    public static Guid? ResolveSecretRecordId(
        AgentFrameworkProviderProfileEditorModel model,
        Guid? fallbackSecretRecordId)
    {
        ArgumentNullException.ThrowIfNull(model);

        var configuration = ParseObject(model.ConfigurationJson);
        if (configuration[SecretRecordIdPropertyName] is JsonValue value &&
            value.TryGetValue<string>(out var secretValue) &&
            Guid.TryParse(secretValue, out var secretId))
        {
            return secretId;
        }

        const string SecretPrefix = "secret:";
        if (!string.IsNullOrWhiteSpace(model.ApiKeyEnvironmentVariable) &&
            model.ApiKeyEnvironmentVariable.StartsWith(SecretPrefix, StringComparison.OrdinalIgnoreCase) &&
            Guid.TryParse(model.ApiKeyEnvironmentVariable[SecretPrefix.Length..], out var inlineSecretId))
        {
            return inlineSecretId;
        }

        return fallbackSecretRecordId;
    }

    public static string ResolveConnectorPluginKey(
        AgentFrameworkProviderProfileEditorModel model,
        WorkspaceProviderProfile? current)
    {
        ArgumentNullException.ThrowIfNull(model);

        var configuration = ParseObject(model.ConfigurationJson);
        if (configuration[ConnectorPluginKeyPropertyName] is JsonValue value &&
            value.TryGetValue<string>(out var configuredPluginKey) &&
            !string.IsNullOrWhiteSpace(configuredPluginKey))
        {
            return configuredPluginKey.Trim();
        }

        if (!string.IsNullOrWhiteSpace(current?.ConnectorPluginKey))
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

        var configuration = ParseObject(model.ConfigurationJson);
        if (configuration[ConfigSchemaVersionPropertyName] is JsonValue value &&
            value.TryGetValue<string>(out var configuredVersion) &&
            !string.IsNullOrWhiteSpace(configuredVersion))
        {
            return configuredVersion.Trim();
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

        var configuration = ParseObject(model.ConfigurationJson);
        if (configuration[TimeoutSecondsPropertyName] is JsonValue value &&
            value.TryGetValue<int>(out var timeoutSeconds))
        {
            return Math.Max(5, timeoutSeconds);
        }

        return Math.Max(5, fallbackValue);
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
}
