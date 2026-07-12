using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Mcp;

public sealed record McpMemoryProviderConfiguration(
    McpServerDescriptor Descriptor,
    McpMemoryProviderToolMap ToolMap)
{
    public static McpMemoryProviderConfiguration FromProfile(
        MemoryProviderProfile profile,
        McpMemoryProviderOptions options)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        var values = profile.Manifest.Extensions.Values;
        var serverKey = McpServerKey.Create(ReadRequiredString(values, McpMemoryProviderConfigurationKeys.ServerKey));
        var displayName = ReadString(values, McpMemoryProviderConfigurationKeys.DisplayName) ?? profile.DisplayName;
        var description = ReadString(values, McpMemoryProviderConfigurationKeys.Description) ?? "MCP memory provider.";
        RejectUnsupportedTool(values, McpMemoryProviderConfigurationKeys.IngestionTool);
        RejectUnsupportedTool(values, McpMemoryProviderConfigurationKeys.SourceRequestTool);
        RejectUnsupportedTool(values, McpMemoryProviderConfigurationKeys.FeedbackTool);
        RejectUnsupportedTool(values, McpMemoryProviderConfigurationKeys.EventPollTool);
        var toolMap = new McpMemoryProviderToolMap(
            ReadTool(values, McpMemoryProviderConfigurationKeys.ContextQueryTool),
            ReadTool(values, McpMemoryProviderConfigurationKeys.OperationStatusTool));
        var descriptor = CreateDescriptor(
            values,
            serverKey,
            displayName,
            description,
            toolMap.AllowedTools,
            options.DefaultTimeout);
        return new McpMemoryProviderConfiguration(descriptor, toolMap);
    }

    private static McpServerDescriptor CreateDescriptor(
        IReadOnlyDictionary<string, JsonElement> values,
        McpServerKey serverKey,
        string displayName,
        string description,
        IReadOnlySet<McpToolName> allowedTools,
        TimeSpan timeout)
    {
        var descriptorKind = ReadString(values, McpMemoryProviderConfigurationKeys.DescriptorKind) ??
            McpMemoryProviderDescriptorKinds.RemoteHttp;
        var identity = new CapabilityIdentity(CapabilityKind.McpServer, CapabilityKey.Create(serverKey.Value));
        var sideEffects = new CapabilitySideEffectProfile(
            CapabilitySideEffectKind.McpTool,
            RequiresApprovalByDefault: false,
            IsStateChanging: false);
        return descriptorKind switch
        {
            McpMemoryProviderDescriptorKinds.RemoteHttp => new RemoteHttpMcpServerDescriptor(
                identity,
                serverKey,
                displayName,
                description,
                Tags: new HashSet<CapabilityTag>(),
                OperationClassifications: new HashSet<CapabilityOperationClassification>
                {
                    CapabilityOperationClassification.McpTool
                },
                sideEffects,
                CapabilityAvailabilityState.Available,
                allowedTools,
                McpApprovalMode.NeverRequire,
                timeout,
                ReadRequiredUri(values, McpMemoryProviderConfigurationKeys.RemoteEndpoint),
                HeaderBindings: CreateHeaderBindings(values),
                RawHeaders: new Dictionary<string, string>()),
            McpMemoryProviderDescriptorKinds.InternalHosted => throw new InvalidOperationException(
                "Internal-hosted MCP memory providers are not executable by the runtime. Configure a remote HTTP MCP endpoint instead."),
            _ => throw new InvalidOperationException($"MCP memory provider descriptor kind '{descriptorKind}' is not supported.")
        };
    }

    private static IReadOnlyDictionary<string, string> CreateHeaderBindings(
        IReadOnlyDictionary<string, JsonElement> values)
    {
        var environmentVariable = ReadString(values, McpMemoryProviderConfigurationKeys.AuthHeaderEnvironmentVariable);
        var configuredHeaderName = ReadString(values, McpMemoryProviderConfigurationKeys.AuthHeaderName);
        if (environmentVariable is null && configuredHeaderName is null)
        {
            return new Dictionary<string, string>();
        }

        if (!McpMemoryProviderHeaderBindingValidator.IsEnvironmentVariableName(environmentVariable))
        {
            throw new InvalidOperationException(
                $"MCP memory provider extension '{McpMemoryProviderConfigurationKeys.AuthHeaderEnvironmentVariable}' must be an environment-variable identifier using ASCII letters, digits, or underscores.");
        }

        var headerName = configuredHeaderName ?? "Authorization";
        if (!McpMemoryProviderHeaderBindingValidator.IsHttpHeaderName(headerName))
        {
            throw new InvalidOperationException(
                $"MCP memory provider extension '{McpMemoryProviderConfigurationKeys.AuthHeaderName}' must be a valid HTTP header token.");
        }

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            [headerName] = environmentVariable!
        };
    }

    private static void RejectUnsupportedTool(
        IReadOnlyDictionary<string, JsonElement> values,
        string key)
    {
        if (!string.IsNullOrWhiteSpace(ReadString(values, key)))
        {
            throw new InvalidOperationException(
                $"MCP memory provider extension '{key}' is not supported by the application runtime and must be removed.");
        }
    }

    private static McpToolName? ReadTool(
        IReadOnlyDictionary<string, JsonElement> values,
        string key)
    {
        var value = ReadString(values, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            McpToolName? missingTool = null;
            return missingTool;
        }

        return McpToolName.Create(value);
    }

    private static Uri ReadRequiredUri(
        IReadOnlyDictionary<string, JsonElement> values,
        string key)
    {
        var value = ReadRequiredString(values, key);
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment) ||
            !IsSecureRemoteEndpoint(uri))
        {
            throw new InvalidOperationException(
                $"MCP memory provider extension '{key}' must be an absolute HTTPS URI without embedded credentials, query strings, or fragments; loopback HTTP is allowed for local development.");
        }

        return uri;
    }

    private static bool IsSecureRemoteEndpoint(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttps ||
            (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
    }

    private static string ReadRequiredString(
        IReadOnlyDictionary<string, JsonElement> values,
        string key) =>
        ReadString(values, key) ?? throw new InvalidOperationException($"MCP memory provider profile is missing required extension '{key}'.");

    private static string? ReadString(
        IReadOnlyDictionary<string, JsonElement> values,
        string key)
    {
        if (!values.TryGetValue(key, out var value) || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            string? missingValue = null;
            return missingValue;
        }

        return value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : throw new InvalidOperationException($"MCP memory provider extension '{key}' must be a string.");
    }
}
