using System.Text.Json;
using System.Text.Json.Nodes;

namespace CanDoItAll.AgentFramework.Models;

public enum AgentA2AProtocolBindingPreference
{
    Auto = 0,
    HttpJson = 1,
    JsonRpc = 2
}

public enum AgentA2AAuthenticationKind
{
    None = 0,
    BearerToken = 1
}

public sealed class AgentA2ASettings
{
    public List<AgentA2ARemoteEndpointSettings> RemoteEndpoints { get; set; } = [];

    public AgentA2AHostingSettings Hosting { get; set; } = new();
}

public sealed class AgentA2ARemoteEndpointSettings
{
    public string EndpointId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string BaseUri { get; set; } = string.Empty;

    public string AgentCardPath { get; set; } = AgentA2AMetadata.DefaultAgentCardPath;

    public AgentA2AProtocolBindingPreference ProtocolBinding { get; set; } = AgentA2AProtocolBindingPreference.Auto;

    public AgentA2AAuthenticationKind Authentication { get; set; } = AgentA2AAuthenticationKind.None;

    public string AuthSecretConfigurationKey { get; set; } = string.Empty;

    public bool ExposeSkillsAsTools { get; set; } = true;

    public string ToolNamePrefix { get; set; } = AgentA2AMetadata.DefaultToolNamePrefix;

    public List<string> AllowedSkillNames { get; set; } = [];

    public int TimeoutSeconds { get; set; } = AgentA2AMetadata.DefaultTimeoutSeconds;
}

public sealed class AgentA2AHostingSettings
{
    public bool Enabled { get; set; }

    public string PathPrefix { get; set; } = AgentA2AMetadata.DefaultHostingPathPrefix;

    public string PublicBaseUri { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0.0";

    public string SkillName { get; set; } = string.Empty;

    public string SkillDescription { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = [];

    public List<AgentA2AProtocolBindingPreference> ProtocolBindings { get; set; } = [];
}

public sealed record AgentA2AValidationResult(
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings)
{
    public bool Succeeded => Errors.Count == 0;
}

public static class AgentA2AMetadata
{
    public const string RootPropertyName = "a2a";
    public const string DefaultAgentCardPath = "/.well-known/agent-card.json";
    public const string DefaultToolNamePrefix = "a2a";
    public const string DefaultHostingPathPrefix = "/a2a";
    public const int DefaultTimeoutSeconds = 30;
    public const int MinimumTimeoutSeconds = 5;
    public const int MaximumTimeoutSeconds = 300;

    private const string RemoteEndpointsPropertyName = "remoteEndpoints";
    private const string HostingPropertyName = "hosting";
    private const string EndpointIdPropertyName = "endpointId";
    private const string DisplayNamePropertyName = "displayName";
    private const string EnabledPropertyName = "enabled";
    private const string BaseUriPropertyName = "baseUri";
    private const string AgentCardPathPropertyName = "agentCardPath";
    private const string ProtocolBindingPropertyName = "protocolBinding";
    private const string AuthenticationPropertyName = "authentication";
    private const string AuthSecretConfigurationKeyPropertyName = "authSecretConfigurationKey";
    private const string ExposeSkillsAsToolsPropertyName = "exposeSkillsAsTools";
    private const string ToolNamePrefixPropertyName = "toolNamePrefix";
    private const string AllowedSkillNamesPropertyName = "allowedSkillNames";
    private const string TimeoutSecondsPropertyName = "timeoutSeconds";
    private const string PathPrefixPropertyName = "pathPrefix";
    private const string PublicBaseUriPropertyName = "publicBaseUri";
    private const string VersionPropertyName = "version";
    private const string SkillNamePropertyName = "skillName";
    private const string SkillDescriptionPropertyName = "skillDescription";
    private const string TagsPropertyName = "tags";
    private const string ProtocolBindingsPropertyName = "protocolBindings";

    public static AgentA2ASettings Read(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return Normalize(new AgentA2ASettings());
        }

        try
        {
            var root = JsonNode.Parse(configurationJson)?.AsObject();
            var a2a = root?[RootPropertyName]?.AsObject();
            if (a2a is null)
            {
                return Normalize(new AgentA2ASettings());
            }

            var settings = new AgentA2ASettings();
            if (a2a[RemoteEndpointsPropertyName] is JsonArray endpoints)
            {
                foreach (var endpointNode in endpoints.OfType<JsonObject>())
                {
                    settings.RemoteEndpoints.Add(ReadEndpoint(endpointNode));
                }
            }

            if (a2a[HostingPropertyName] is JsonObject hosting)
            {
                settings.Hosting = ReadHosting(hosting);
            }

            return Normalize(settings);
        }
        catch (JsonException)
        {
            return Normalize(new AgentA2ASettings());
        }
    }

    public static string Write(
        string? configurationJson,
        AgentA2ASettings? settings)
    {
        var normalized = Normalize(settings ?? new AgentA2ASettings());
        var root = ParseObject(configurationJson);
        if (IsDefault(normalized))
        {
            root.Remove(RootPropertyName);
            return root.ToJsonString();
        }

        root[RootPropertyName] = new JsonObject
        {
            [RemoteEndpointsPropertyName] = new JsonArray(
                normalized.RemoteEndpoints
                    .Select(WriteEndpoint)
                    .ToArray<JsonNode?>()),
            [HostingPropertyName] = WriteHosting(normalized.Hosting)
        };

        return root.ToJsonString();
    }

    public static AgentA2ASettings Normalize(AgentA2ASettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var endpoints = settings.RemoteEndpoints
            .Where(endpoint => endpoint is not null)
            .Select(NormalizeEndpoint)
            .GroupBy(endpoint => endpoint.EndpointId, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .OrderBy(endpoint => endpoint.EndpointId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new AgentA2ASettings
        {
            RemoteEndpoints = endpoints,
            Hosting = NormalizeHosting(settings.Hosting)
        };
    }

    public static AgentA2AValidationResult Validate(AgentA2ASettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var errors = new List<string>();
        var warnings = new List<string>();
        var endpointIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var endpoints = settings.RemoteEndpoints
            .Where(endpoint => endpoint is not null)
            .Select(NormalizeEndpoint)
            .ToList();
        foreach (var endpoint in endpoints)
        {
            if (!endpointIds.Add(endpoint.EndpointId))
            {
                errors.Add($"A2A endpoint id '{endpoint.EndpointId}' is duplicated.");
            }

            if (!endpoint.Enabled)
            {
                continue;
            }

            if (!TryCreateHttpUri(endpoint.BaseUri, out _))
            {
                errors.Add($"A2A endpoint '{endpoint.EndpointId}' must use an absolute http or https baseUri.");
            }

            if (string.IsNullOrWhiteSpace(endpoint.AgentCardPath) ||
                !endpoint.AgentCardPath.StartsWith("/", StringComparison.Ordinal))
            {
                errors.Add($"A2A endpoint '{endpoint.EndpointId}' must use an absolute agentCardPath.");
            }

            if (endpoint.Authentication == AgentA2AAuthenticationKind.BearerToken &&
                string.IsNullOrWhiteSpace(endpoint.AuthSecretConfigurationKey))
            {
                errors.Add($"A2A endpoint '{endpoint.EndpointId}' uses bearer auth but does not name an auth secret configuration key.");
            }

            if (LooksLikeRawSecret(endpoint.AuthSecretConfigurationKey))
            {
                errors.Add($"A2A endpoint '{endpoint.EndpointId}' appears to store a raw secret. Store only a configuration key or secret reference.");
            }

            if (!IsValidFunctionIdentifier(endpoint.ToolNamePrefix))
            {
                errors.Add($"A2A endpoint '{endpoint.EndpointId}' has an invalid toolNamePrefix '{endpoint.ToolNamePrefix}'. Use letters, digits, or underscores, and start with a letter or underscore.");
            }

            if (endpoint.TimeoutSeconds is < MinimumTimeoutSeconds or > MaximumTimeoutSeconds)
            {
                errors.Add($"A2A endpoint '{endpoint.EndpointId}' timeoutSeconds must be between {MinimumTimeoutSeconds} and {MaximumTimeoutSeconds}.");
            }
        }

        var hosting = NormalizeHosting(settings.Hosting);
        if (hosting.Enabled)
        {
            if (string.IsNullOrWhiteSpace(hosting.PathPrefix) ||
                !hosting.PathPrefix.StartsWith("/", StringComparison.Ordinal))
            {
                errors.Add("A2A hosting pathPrefix must be explicit and start with '/'.");
            }

            if (!string.IsNullOrWhiteSpace(hosting.PublicBaseUri) &&
                !TryCreateHttpUri(hosting.PublicBaseUri, out _))
            {
                errors.Add("A2A hosting publicBaseUri must use an absolute http or https URI.");
            }

            if (string.IsNullOrWhiteSpace(hosting.SkillName))
            {
                warnings.Add("A2A hosting is enabled without an explicit skillName; the agent name will be used when a card is generated.");
            }
        }

        return new AgentA2AValidationResult(errors, warnings);
    }

    public static AgentA2AValidationResult Validate(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
        {
            return Validate(new AgentA2ASettings());
        }

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return document.RootElement.TryGetProperty(RootPropertyName, out _)
                ? Validate(Read(configurationJson))
                : Validate(new AgentA2ASettings());
        }
        catch (JsonException exception)
        {
            return new AgentA2AValidationResult(
                [$"Agent configuration JSON is invalid: {exception.Message}"],
                []);
        }
    }

    public static string NormalizeToolNamePrefix(string? value)
    {
        return NormalizeFunctionIdentifier(value, DefaultToolNamePrefix);
    }

    public static string NormalizeEndpointId(string? value)
    {
        return NormalizeFunctionIdentifier(value, string.Empty);
    }

    public static bool IsValidFunctionIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (!char.IsLetter(trimmed[0]) && trimmed[0] != '_')
        {
            return false;
        }

        return trimmed.All(character =>
            char.IsLetterOrDigit(character) ||
            character == '_');
    }

    private static AgentA2ARemoteEndpointSettings ReadEndpoint(JsonObject endpoint)
    {
        return new AgentA2ARemoteEndpointSettings
        {
            EndpointId = TryReadString(endpoint, EndpointIdPropertyName),
            DisplayName = TryReadString(endpoint, DisplayNamePropertyName),
            Enabled = TryReadBoolean(endpoint, EnabledPropertyName, defaultValue: true),
            BaseUri = TryReadString(endpoint, BaseUriPropertyName),
            AgentCardPath = TryReadString(endpoint, AgentCardPathPropertyName),
            ProtocolBinding = TryReadProtocolBinding(endpoint[ProtocolBindingPropertyName], AgentA2AProtocolBindingPreference.Auto),
            Authentication = TryReadAuthentication(endpoint[AuthenticationPropertyName], AgentA2AAuthenticationKind.None),
            AuthSecretConfigurationKey = TryReadString(endpoint, AuthSecretConfigurationKeyPropertyName),
            ExposeSkillsAsTools = TryReadBoolean(endpoint, ExposeSkillsAsToolsPropertyName, defaultValue: true),
            ToolNamePrefix = TryReadString(endpoint, ToolNamePrefixPropertyName),
            AllowedSkillNames = ReadStringList(endpoint[AllowedSkillNamesPropertyName]),
            TimeoutSeconds = TryReadInt32(endpoint, TimeoutSecondsPropertyName, DefaultTimeoutSeconds)
        };
    }

    private static AgentA2AHostingSettings ReadHosting(JsonObject hosting)
    {
        return new AgentA2AHostingSettings
        {
            Enabled = TryReadBoolean(hosting, EnabledPropertyName),
            PathPrefix = TryReadString(hosting, PathPrefixPropertyName),
            PublicBaseUri = TryReadString(hosting, PublicBaseUriPropertyName),
            Version = TryReadString(hosting, VersionPropertyName),
            SkillName = TryReadString(hosting, SkillNamePropertyName),
            SkillDescription = TryReadString(hosting, SkillDescriptionPropertyName),
            Tags = ReadStringList(hosting[TagsPropertyName]),
            ProtocolBindings = ReadProtocolBindingList(hosting[ProtocolBindingsPropertyName])
        };
    }

    private static AgentA2ARemoteEndpointSettings NormalizeEndpoint(AgentA2ARemoteEndpointSettings endpoint)
    {
        var displayName = NormalizeText(endpoint.DisplayName);
        var baseUri = NormalizeText(endpoint.BaseUri);
        var endpointId = NormalizeEndpointId(endpoint.EndpointId);
        if (string.IsNullOrWhiteSpace(endpointId))
        {
            endpointId = NormalizeEndpointId(displayName);
        }

        if (string.IsNullOrWhiteSpace(endpointId))
        {
            endpointId = NormalizeEndpointId(baseUri);
        }

        var toolNamePrefix = NormalizeToolNamePrefix(endpoint.ToolNamePrefix);
        return new AgentA2ARemoteEndpointSettings
        {
            EndpointId = string.IsNullOrWhiteSpace(endpointId) ? DefaultToolNamePrefix : endpointId,
            DisplayName = displayName,
            Enabled = endpoint.Enabled,
            BaseUri = NormalizeUriText(baseUri),
            AgentCardPath = NormalizePath(endpoint.AgentCardPath, DefaultAgentCardPath),
            ProtocolBinding = endpoint.ProtocolBinding,
            Authentication = endpoint.Authentication,
            AuthSecretConfigurationKey = NormalizeText(endpoint.AuthSecretConfigurationKey),
            ExposeSkillsAsTools = endpoint.ExposeSkillsAsTools,
            ToolNamePrefix = toolNamePrefix,
            AllowedSkillNames = NormalizeDistinct(endpoint.AllowedSkillNames),
            TimeoutSeconds = endpoint.TimeoutSeconds
        };
    }

    private static AgentA2AHostingSettings NormalizeHosting(AgentA2AHostingSettings? hosting)
    {
        hosting ??= new AgentA2AHostingSettings();
        var protocolBindings = hosting.ProtocolBindings.Count == 0
            ? [AgentA2AProtocolBindingPreference.HttpJson, AgentA2AProtocolBindingPreference.JsonRpc]
            : hosting.ProtocolBindings
                .Where(binding => binding != AgentA2AProtocolBindingPreference.Auto)
                .Distinct()
                .ToList();

        return new AgentA2AHostingSettings
        {
            Enabled = hosting.Enabled,
            PathPrefix = NormalizePath(hosting.PathPrefix, DefaultHostingPathPrefix),
            PublicBaseUri = NormalizeUriText(hosting.PublicBaseUri),
            Version = string.IsNullOrWhiteSpace(hosting.Version) ? "1.0.0" : hosting.Version.Trim(),
            SkillName = NormalizeText(hosting.SkillName),
            SkillDescription = NormalizeText(hosting.SkillDescription),
            Tags = NormalizeDistinct(hosting.Tags),
            ProtocolBindings = protocolBindings
        };
    }

    private static JsonObject WriteEndpoint(AgentA2ARemoteEndpointSettings endpoint)
    {
        return new JsonObject
        {
            [EndpointIdPropertyName] = endpoint.EndpointId,
            [DisplayNamePropertyName] = endpoint.DisplayName,
            [EnabledPropertyName] = endpoint.Enabled,
            [BaseUriPropertyName] = endpoint.BaseUri,
            [AgentCardPathPropertyName] = endpoint.AgentCardPath,
            [ProtocolBindingPropertyName] = endpoint.ProtocolBinding.ToString(),
            [AuthenticationPropertyName] = endpoint.Authentication.ToString(),
            [AuthSecretConfigurationKeyPropertyName] = endpoint.AuthSecretConfigurationKey,
            [ExposeSkillsAsToolsPropertyName] = endpoint.ExposeSkillsAsTools,
            [ToolNamePrefixPropertyName] = endpoint.ToolNamePrefix,
            [AllowedSkillNamesPropertyName] = new JsonArray(
                endpoint.AllowedSkillNames
                    .Select(name => JsonValue.Create(name))
                    .ToArray()),
            [TimeoutSecondsPropertyName] = endpoint.TimeoutSeconds
        };
    }

    private static JsonObject WriteHosting(AgentA2AHostingSettings hosting)
    {
        return new JsonObject
        {
            [EnabledPropertyName] = hosting.Enabled,
            [PathPrefixPropertyName] = hosting.PathPrefix,
            [PublicBaseUriPropertyName] = hosting.PublicBaseUri,
            [VersionPropertyName] = hosting.Version,
            [SkillNamePropertyName] = hosting.SkillName,
            [SkillDescriptionPropertyName] = hosting.SkillDescription,
            [TagsPropertyName] = new JsonArray(
                hosting.Tags
                    .Select(tag => JsonValue.Create(tag))
                    .ToArray()),
            [ProtocolBindingsPropertyName] = new JsonArray(
                hosting.ProtocolBindings
                    .Select(binding => JsonValue.Create(binding.ToString()))
                    .ToArray())
        };
    }

    private static bool IsDefault(AgentA2ASettings settings)
    {
        return settings.RemoteEndpoints.Count == 0 &&
               !settings.Hosting.Enabled &&
               string.Equals(settings.Hosting.PathPrefix, DefaultHostingPathPrefix, StringComparison.Ordinal) &&
               string.IsNullOrWhiteSpace(settings.Hosting.PublicBaseUri) &&
               string.Equals(settings.Hosting.Version, "1.0.0", StringComparison.Ordinal) &&
               string.IsNullOrWhiteSpace(settings.Hosting.SkillName) &&
               string.IsNullOrWhiteSpace(settings.Hosting.SkillDescription) &&
               settings.Hosting.Tags.Count == 0;
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

    private static bool TryReadBoolean(JsonObject node, string propertyName, bool defaultValue = false)
    {
        return node[propertyName] is JsonValue value && value.TryGetValue<bool>(out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private static string TryReadString(JsonObject node, string propertyName)
    {
        return node[propertyName] is JsonValue value && value.TryGetValue<string>(out var parsedValue)
            ? parsedValue
            : string.Empty;
    }

    private static int TryReadInt32(JsonObject node, string propertyName, int defaultValue)
    {
        return node[propertyName] is JsonValue value && value.TryGetValue<int>(out var parsedValue)
            ? parsedValue
            : defaultValue;
    }

    private static AgentA2AProtocolBindingPreference TryReadProtocolBinding(JsonNode? node, AgentA2AProtocolBindingPreference defaultValue)
    {
        return ReadEnum(node, defaultValue);
    }

    private static AgentA2AAuthenticationKind TryReadAuthentication(JsonNode? node, AgentA2AAuthenticationKind defaultValue)
    {
        return ReadEnum(node, defaultValue);
    }

    private static TEnum ReadEnum<TEnum>(JsonNode? node, TEnum defaultValue)
        where TEnum : struct, Enum
    {
        if (node is not JsonValue value)
        {
            return defaultValue;
        }

        if (value.TryGetValue<string>(out var text) &&
            Enum.TryParse<TEnum>(text, ignoreCase: true, out var parsedText))
        {
            return parsedText;
        }

        if (value.TryGetValue<int>(out var numeric) &&
            Enum.IsDefined(typeof(TEnum), numeric))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), numeric);
        }

        return defaultValue;
    }

    private static List<string> ReadStringList(JsonNode? node)
    {
        return node is JsonArray array
            ? NormalizeDistinct(
                array
                    .Select(item => item?.GetValue<string>())
                    .Where(item => !string.IsNullOrWhiteSpace(item))
                    .Cast<string>())
            : [];
    }

    private static List<AgentA2AProtocolBindingPreference> ReadProtocolBindingList(JsonNode? node)
    {
        return node is JsonArray array
            ? array
                .Select(item => ReadEnum(item, AgentA2AProtocolBindingPreference.Auto))
                .Where(item => item != AgentA2AProtocolBindingPreference.Auto)
                .Distinct()
                .ToList()
            : [];
    }

    private static List<string> NormalizeDistinct(IEnumerable<string>? values)
    {
        return values?
            .Select(NormalizeText)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];
    }

    private static string NormalizeFunctionIdentifier(string? value, string fallback)
    {
        var normalized = new string(
                (value ?? string.Empty)
                .Trim()
                .Select(character => char.IsLetterOrDigit(character) ? character : '_')
                .ToArray())
            .Trim('_');

        if (string.IsNullOrWhiteSpace(normalized))
        {
            return fallback;
        }

        return char.IsLetter(normalized[0]) || normalized[0] == '_'
            ? normalized
            : "_" + normalized;
    }

    private static string NormalizeText(string? value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static string NormalizeUriText(string? value)
    {
        return NormalizeText(value).TrimEnd('/');
    }

    private static string NormalizePath(string? value, string fallback)
    {
        var path = string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();

        return path.StartsWith("/", StringComparison.Ordinal)
            ? path
            : "/" + path;
    }

    private static bool TryCreateHttpUri(string value, out Uri? uri)
    {
        uri = null;
        if (!Uri.TryCreate(value, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (parsed.Scheme is not ("http" or "https"))
        {
            return false;
        }

        uri = parsed;
        return true;
    }

    private static bool LooksLikeRawSecret(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("sk-", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return trimmed.Length >= 32 &&
               !trimmed.Contains(':', StringComparison.Ordinal) &&
               !trimmed.Contains('/', StringComparison.Ordinal) &&
               !trimmed.Contains('\\', StringComparison.Ordinal) &&
               trimmed.Any(char.IsLetter) &&
               trimmed.Any(char.IsDigit);
    }
}
