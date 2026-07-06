using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Mcp;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryMcpDriverTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task SB08_MCP001_Context_query_calls_configured_mcp_tool_with_structured_payload()
    {
        var operation = CreateOperation(MemoryCapabilityIds.ContextQuerySync);
        var contextPack = CreateContextPack();
        var factory = new RecordingMcpClientFactory(
            new Dictionary<McpToolName, string>
            {
                [McpToolName.Create("memory_context_query")] =
                    JsonSerializer.Serialize(McpMemoryProviderResponse.FromContextPack(contextPack), JsonOptions)
            });
        var driver = new McpMemoryProviderDriver(factory, new McpMemoryProviderOptions());
        var provider = CreateProfile(
            [MemoryCapabilityIds.ContextQuerySync],
            (McpMemoryProviderConfigurationKeys.ContextQueryTool, JsonString("memory_context_query")));

        var result = await driver.ExecuteContextQueryAsync(
            provider,
            operation,
            new MemoryContextQueryRequest(
                "What should I remember?",
                [MemoryCapabilityIds.ContextQuerySync],
                MemorySourceProvenance.None),
            CancellationToken.None);

        Assert.Equal(MemoryProviderDriverResultKind.ContextPack, result.Kind);
        Assert.Equal(contextPack.ContextPackId, result.ContextPack?.ContextPackId);
        Assert.Equal(McpServerDescriptorKind.RemoteHttp, factory.LastDescriptor?.DescriptorKind);
        Assert.Equal(McpServerKey.Create("memory-mcp"), factory.LastDescriptor?.ServerKey);
        Assert.Contains(McpToolName.Create("memory_context_query"), factory.LastDescriptor!.AllowedTools);
        Assert.Equal(1, factory.CreateCount);
        Assert.True(factory.LastClient!.Started);
        Assert.True(factory.LastClient.Stopped);
        var call = Assert.Single(factory.LastClient.ToolCalls);
        Assert.Equal(McpToolName.Create("memory_context_query"), call.ToolName);

        using var arguments = JsonDocument.Parse(call.JsonArguments);
        var root = arguments.RootElement;
        Assert.Equal("What should I remember?", root.GetProperty("query").GetString());
        Assert.Equal(operation.OperationId.Value.ToString("D"), root.GetProperty("operationId").GetString());
        Assert.Equal(operation.CorrelationId.Value.ToString("D"), root.GetProperty("correlationId").GetString());
        Assert.Equal(provider.InstanceId.Value, root.GetProperty("providerInstanceId").GetString());
        Assert.Equal(MemoryProtocolVersion.Current.Value, root.GetProperty("memoryProtocolVersion").GetString());
        Assert.Equal(MemoryCapabilityIds.ContextQuerySync.Value, root.GetProperty("capabilityId").GetString());
        Assert.True(root.TryGetProperty("envelope", out var envelope));
        Assert.Equal(
            MemoryCapabilityIds.ContextQuerySync.Value,
            envelope.GetProperty("payload").GetProperty("requestedCapabilities")[0].GetProperty("value").GetString());
    }

    [Fact]
    public async Task SB08_MCP002_Unsupported_ingestion_is_structured_when_tool_not_configured()
    {
        var factory = new RecordingMcpClientFactory(new Dictionary<McpToolName, string>());
        var driver = new McpMemoryProviderDriver(factory, new McpMemoryProviderOptions());
        var provider = CreateProfile(
            [MemoryCapabilityIds.ContextQuerySync, MemoryCapabilityIds.IngestionSnapshot],
            (McpMemoryProviderConfigurationKeys.ContextQueryTool, JsonString("memory_context_query")));

        var result = await driver.ExecuteIngestionAsync(
            provider,
            CreateOperation(MemoryCapabilityIds.IngestionSnapshot, MemoryOperationKind.Ingestion),
            new MemoryIngestionRequest(
                MemorySourceSnapshotId.Parse("project:alpha"),
                MemorySourceKind.Project,
                MemoryPayload.FromText("source text"),
                [MemoryCapabilityIds.IngestionSnapshot]),
            CancellationToken.None);

        Assert.Equal(McpMemoryAdapterResultKind.UnsupportedCapability, result.Kind);
        Assert.Null(result.AcceptedOperation);
        Assert.Contains("ingestion.snapshot", result.Diagnostic, StringComparison.Ordinal);
        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public async Task SB08_MCP003_Async_status_tool_returns_operation_result()
    {
        var operation = CreateOperation(MemoryCapabilityIds.ContextQueryAsync);
        var operationResult = new MemoryOperationResult(
            operation.OperationId,
            MemoryOperationStatus.Succeeded,
            MemoryPayload.FromText("done"),
            [],
            [],
            ["source://alpha"]);
        var factory = new RecordingMcpClientFactory(
            new Dictionary<McpToolName, string>
            {
                [McpToolName.Create("memory_operation_status")] =
                    JsonSerializer.Serialize(McpMemoryOperationStatusToolResponse.FromResult(operationResult), JsonOptions)
            });
        var driver = new McpMemoryProviderDriver(factory, new McpMemoryProviderOptions());
        var provider = CreateProfile(
            [MemoryCapabilityIds.ContextQueryAsync],
            (McpMemoryProviderConfigurationKeys.OperationStatusTool, JsonString("memory_operation_status")));

        var result = await driver.GetOperationStatusAsync(
            provider,
            new MemoryOperationStatusRequest(operation.OperationId),
            CancellationToken.None);

        Assert.Equal(McpMemoryAdapterResultKind.OperationResult, result.Kind);
        Assert.Equal(MemoryOperationStatus.Succeeded, result.OperationResult?.Status);
        var call = Assert.Single(factory.LastClient!.ToolCalls);
        Assert.Equal(McpToolName.Create("memory_operation_status"), call.ToolName);
        using var arguments = JsonDocument.Parse(call.JsonArguments);
        Assert.Equal(operation.OperationId.Value.ToString("D"), arguments.RootElement.GetProperty("operationId").GetString());
    }

    [Fact]
    public async Task SB08_MCP004_Event_polling_returns_provider_events_when_tool_available()
    {
        var providerEvent = new MemoryProviderEvent(
            MemoryProviderEventId.New(),
            MemoryProviderEventKind.MaintenanceSignal,
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            "Rebuild memory index.",
            MemoryPayload.FromText("maintenance"));
        var factory = new RecordingMcpClientFactory(
            new Dictionary<McpToolName, string>
            {
                [McpToolName.Create("memory_poll_events")] =
                    JsonSerializer.Serialize(new McpMemoryProviderEventPollResponse([providerEvent]), JsonOptions)
            });
        var driver = new McpMemoryProviderDriver(factory, new McpMemoryProviderOptions());
        var provider = CreateProfile(
            [MemoryCapabilityIds.EventsHostPoll],
            (McpMemoryProviderConfigurationKeys.EventPollTool, JsonString("memory_poll_events")));

        var result = await driver.PollEventsAsync(provider, CancellationToken.None);

        Assert.Equal(McpMemoryAdapterResultKind.ProviderEvents, result.Kind);
        var returned = Assert.Single(result.Events);
        Assert.Equal(providerEvent.EventId, returned.EventId);
        Assert.Equal(McpToolName.Create("memory_poll_events"), Assert.Single(factory.LastClient!.ToolCalls).ToolName);
    }

    [Fact]
    public async Task SB08_MCP005_Missing_query_tool_maps_unsupported_capability_without_mcp_call()
    {
        var factory = new RecordingMcpClientFactory(new Dictionary<McpToolName, string>());
        var driver = new McpMemoryProviderDriver(factory, new McpMemoryProviderOptions());
        var provider = CreateProfile([MemoryCapabilityIds.ContextQuerySync]);

        var result = await driver.ExecuteContextQueryAsync(
            provider,
            CreateOperation(MemoryCapabilityIds.ContextQuerySync),
            new MemoryContextQueryRequest(
                "No tool configured.",
                [MemoryCapabilityIds.ContextQuerySync],
                MemorySourceProvenance.None),
            CancellationToken.None);

        Assert.Equal(MemoryProviderDriverResultKind.UnsupportedCapability, result.Kind);
        Assert.Equal(MemoryLedgerStatus.Failed, result.LedgerStatus);
        Assert.Equal(0, factory.CreateCount);
    }

    [Fact]
    public void SB08_MCP006_Manifest_mapper_declares_effective_mcp_capability_versions()
    {
        var toolMap = new McpMemoryProviderToolMap(
            ContextQueryTool: McpToolName.Create("memory_context_query"),
            IngestionTool: null,
            SourceRequestTool: null,
            FeedbackTool: null,
            EventPollTool: McpToolName.Create("memory_poll_events"),
            OperationStatusTool: McpToolName.Create("memory_operation_status"));

        var manifest = McpMemoryProviderManifestFactory.CreateManifest(
            MemoryProviderKind.Parse("memory.mcp"),
            toolMap,
            MemoryProviderLimits.Default);

        Assert.Contains(
            manifest.Capabilities,
            capability => capability.Id == MemoryCapabilityIds.ContextQuerySync &&
                capability.Version == McpMemoryCapabilityVersions.ToolV1 &&
                capability.Supported);
        Assert.Contains(
            manifest.Capabilities,
            capability => capability.Id == MemoryCapabilityIds.EventsHostPoll &&
                capability.Version == McpMemoryCapabilityVersions.ToolV1 &&
                capability.Supported);
        Assert.DoesNotContain(manifest.Capabilities, capability => capability.Id == MemoryCapabilityIds.IngestionSnapshot);
        Assert.True(manifest.InteractionSupport.SupportsSynchronousQueries);
        Assert.True(manifest.InteractionSupport.SupportsAsynchronousOperations);
        Assert.True(manifest.InteractionSupport.SupportsProviderEvents);
    }

    private static MemoryProviderProfile CreateProfile(
        IReadOnlyList<MemoryCapabilityId> capabilities,
        params (string Key, JsonElement Value)[] extensions)
    {
        var values = new List<(string Key, JsonElement Value)>
        {
            (McpMemoryProviderConfigurationKeys.DescriptorKind, JsonString(McpMemoryProviderDescriptorKinds.RemoteHttp)),
            (McpMemoryProviderConfigurationKeys.ServerKey, JsonString("memory-mcp")),
            (McpMemoryProviderConfigurationKeys.RemoteEndpoint, JsonString("https://memory-mcp.test/mcp")),
            (McpMemoryProviderConfigurationKeys.DisplayName, JsonString("Memory MCP")),
            (McpMemoryProviderConfigurationKeys.Description, JsonString("Test MCP memory provider"))
        };
        values.AddRange(extensions);
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse("mcp-provider"),
            "MCP Provider",
            MemoryProviderDriverKind.Mcp,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: [],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("memory.mcp"),
                MemoryProtocolVersion.Current,
                capabilities.Select(capability => new MemoryCapabilityDescriptor(capability, "1.0", Supported: true)).ToArray(),
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                new MemoryExtensionData(values.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal))));
    }

    private static MemoryOperationRecord CreateOperation(
        MemoryCapabilityId capability,
        MemoryOperationKind operationKind = MemoryOperationKind.ContextQuery)
    {
        var now = new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
        return MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            new MemoryOperationId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            MemoryProviderInstanceId.Parse("mcp-provider"),
            capability,
            operationKind,
            new MemoryLedgerRequester(
                "agent-runtime",
                AgentId: "agent-alpha",
                AgentRole: "programmer",
                SessionId: "session-alpha",
                WorkflowId: "workflow-alpha",
                WorkflowNodeId: "node-alpha",
                ProcessId: "process-alpha",
                ProcessStepId: "step-alpha"),
            new MemoryCorrelationId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new MemoryCausationId(Guid.Parse("33333333-3333-3333-3333-333333333333")),
            [],
            MemoryLedgerRetentionPolicy.Expiring(now.AddHours(1), now.AddHours(2)),
            now);
    }

    private static MemoryContextPack CreateContextPack() =>
        new(
            MemoryContextPackId.New(),
            "Relevant context.",
            [new MemoryContextSection("Design note", "Use generic MCP abstractions.", [], 0.9m)],
            [],
            0.91m,
            FeedbackHandle: null);

    private static JsonElement JsonString(string value) => JsonSerializer.SerializeToElement(value, JsonOptions);

    private sealed class RecordingMcpClientFactory(IReadOnlyDictionary<McpToolName, string> toolResults) : IMcpClientFactory
    {
        public int CreateCount { get; private set; }

        public McpServerDescriptor? LastDescriptor { get; private set; }

        public RecordingMcpRuntimeClient? LastClient { get; private set; }

        public Task<IMcpRuntimeClient> CreateAsync(
            McpServerDescriptor descriptor,
            string correlationId,
            CancellationToken cancellationToken)
        {
            CreateCount++;
            LastDescriptor = descriptor;
            LastClient = new RecordingMcpRuntimeClient(toolResults);
            return Task.FromResult<IMcpRuntimeClient>(LastClient);
        }
    }

    private sealed class RecordingMcpRuntimeClient(IReadOnlyDictionary<McpToolName, string> toolResults) : IMcpRuntimeClient
    {
        public bool Started { get; private set; }

        public bool Stopped { get; private set; }

        public List<ToolCall> ToolCalls { get; } = [];

        public Task StartAsync(CancellationToken cancellationToken)
        {
            Started = true;
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<DiscoveredMcpTool>> ListToolsAsync(CancellationToken cancellationToken)
        {
            IReadOnlyList<DiscoveredMcpTool> tools = toolResults.Keys
                .Select(tool => new DiscoveredMcpTool(tool, $"Tool {tool.Value}"))
                .ToArray();
            return Task.FromResult(tools);
        }

        public Task<string> CallToolAsync(
            McpToolName toolName,
            string jsonArguments,
            CancellationToken cancellationToken)
        {
            ToolCalls.Add(new ToolCall(toolName, jsonArguments));
            return Task.FromResult(toolResults[toolName]);
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            Stopped = true;
            return Task.CompletedTask;
        }
    }

    private sealed record ToolCall(McpToolName ToolName, string JsonArguments);
}
