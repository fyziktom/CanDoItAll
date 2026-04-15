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

    public static string BuildConfigurationJson(
        WorkspaceProviderProfile provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        var configuration = ParseObject(provider.ExtraSettingsJson);
        configuration[ConnectorPluginKeyPropertyName] = provider.ConnectorPluginKey;
        configuration[ConfigSchemaVersionPropertyName] = provider.ConfigSchemaVersion;
        configuration[TimeoutSecondsPropertyName] = provider.TimeoutSeconds;
        if (provider.ApiKeySecretId.HasValue)
        {
            configuration[SecretRecordIdPropertyName] = provider.ApiKeySecretId.Value.ToString("D");
        }
        else
        {
            configuration.Remove(SecretRecordIdPropertyName);
        }

        return configuration.ToJsonString();
    }

    public static string BuildExtraSettingsJson(
        string? configurationJson,
        string connectorPluginKey,
        string configSchemaVersion,
        Guid? secretRecordId,
        int timeoutSeconds)
    {
        var configuration = ParseObject(configurationJson);
        configuration[ConnectorPluginKeyPropertyName] = connectorPluginKey;
        configuration[ConfigSchemaVersionPropertyName] = configSchemaVersion;
        configuration[TimeoutSecondsPropertyName] = timeoutSeconds;
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

        return model.Kind switch
        {
            AgentFrameworkProviderKind.Ollama when LooksLikeLocalOllama(model.BaseUrl) => OllamaProviderAdapter.PluginKey,
            AgentFrameworkProviderKind.Ollama => OllamaRemoteProviderAdapter.PluginKey,
            _ => OpenAiProviderAdapter.PluginKey
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
