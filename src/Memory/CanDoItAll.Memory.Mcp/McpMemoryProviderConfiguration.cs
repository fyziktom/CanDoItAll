using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Mcp;

public static class McpMemoryProviderConfigurationKeys
{
    public const string DescriptorKind = "host.candoitall.memory.mcp.descriptorKind";
    public const string ServerKey = "host.candoitall.memory.mcp.serverKey";
    public const string DisplayName = "host.candoitall.memory.mcp.displayName";
    public const string Description = "host.candoitall.memory.mcp.description";
    public const string RemoteEndpoint = "host.candoitall.memory.mcp.remoteEndpoint";
    public const string ImplementationKey = "host.candoitall.memory.mcp.implementationKey";
    public const string ContextQueryTool = "host.candoitall.memory.mcp.tools.contextQuery";
    public const string IngestionTool = "host.candoitall.memory.mcp.tools.ingestion";
    public const string SourceRequestTool = "host.candoitall.memory.mcp.tools.sourceRequest";
    public const string FeedbackTool = "host.candoitall.memory.mcp.tools.feedback";
    public const string EventPollTool = "host.candoitall.memory.mcp.tools.eventPoll";
    public const string OperationStatusTool = "host.candoitall.memory.mcp.tools.operationStatus";
}

public static class McpMemoryProviderDescriptorKinds
{
    public const string RemoteHttp = "remote-http";
    public const string InternalHosted = "internal-hosted";
}

public sealed record McpMemoryProviderToolMap(
    McpToolName? ContextQueryTool,
    McpToolName? IngestionTool,
    McpToolName? SourceRequestTool,
    McpToolName? FeedbackTool,
    McpToolName? EventPollTool,
    McpToolName? OperationStatusTool)
{
    public IReadOnlySet<McpToolName> AllowedTools =>
        new[]
        {
            ContextQueryTool,
            IngestionTool,
            SourceRequestTool,
            FeedbackTool,
            EventPollTool,
            OperationStatusTool
        }
        .OfType<McpToolName>()
        .ToHashSet();
}

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
        var toolMap = new McpMemoryProviderToolMap(
            ReadTool(values, McpMemoryProviderConfigurationKeys.ContextQueryTool),
            ReadTool(values, McpMemoryProviderConfigurationKeys.IngestionTool),
            ReadTool(values, McpMemoryProviderConfigurationKeys.SourceRequestTool),
            ReadTool(values, McpMemoryProviderConfigurationKeys.FeedbackTool),
            ReadTool(values, McpMemoryProviderConfigurationKeys.EventPollTool),
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
        var sideEffects = new CapabilitySideEffectProfile(CapabilitySideEffectKind.McpTool, RequiresApprovalByDefault: false, IsStateChanging: true);
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
                HeaderBindings: new Dictionary<string, string>(),
                RawHeaders: new Dictionary<string, string>()),
            McpMemoryProviderDescriptorKinds.InternalHosted => new InternalHostedMcpServerDescriptor(
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
                ImplementationKey.Create(ReadRequiredString(values, McpMemoryProviderConfigurationKeys.ImplementationKey))),
            _ => throw new InvalidOperationException($"MCP memory provider descriptor kind '{descriptorKind}' is not supported.")
        };
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
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"MCP memory provider extension '{key}' must be an absolute URI.");
        }

        return uri;
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
