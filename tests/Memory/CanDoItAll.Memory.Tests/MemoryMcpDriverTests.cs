using System.Text.Json;
using CanDoItAll.AgentFramework.Capabilities.Abstractions;
using CanDoItAll.AgentFramework.Mcp.Abstractions;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Mcp;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryMcpDriverTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task MCP001_Context_query_calls_configured_mcp_tool_with_structured_payload()
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
                MemorySourceProvenance.None)
            {
                Context = new MemoryRequestContext(
                    new MemoryWorkspaceContext("workspace-alpha", "Workspace alpha", CustomerId: null, Domain: null, Tags: []),
                    new MemoryExecutionContext(
                        ProjectId: "44444444-4444-4444-4444-444444444444",
                        ProjectName: "Project alpha",
                        ProcessId: "process-alpha",
                        ProcessStepId: "step-alpha",
                        ProcessStepName: null,
                        WorkflowId: "workflow-alpha",
                        WorkflowNodeId: "node-alpha",
                        ArtifactIds: []),
                    MemoryPolicyContext.InternalDefault,
                    MemoryBudget.Default,
                    MemoryExtensionData.Empty)
            },
            CancellationToken.None);

        Assert.Equal(MemoryProviderDriverResultKind.ContextPack, result.Kind);
        Assert.Equal(contextPack.ContextPackId, result.ContextPack?.ContextPackId);
        Assert.Equal(McpServerDescriptorKind.RemoteHttp, factory.LastDescriptor?.DescriptorKind);
        Assert.Equal(McpServerKey.Create("memory-mcp"), factory.LastDescriptor?.ServerKey);
        Assert.Contains(McpToolName.Create("memory_context_query"), factory.LastDescriptor!.AllowedTools);
        var remoteDescriptor = Assert.IsType<RemoteHttpMcpServerDescriptor>(factory.LastDescriptor);
        Assert.Equal("CANDOITALL_MEMORY_MCP_TEST_KEY", remoteDescriptor.HeaderBindings["Authorization"]);
        Assert.False(remoteDescriptor.SideEffectProfile.IsStateChanging);
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
        Assert.Equal(
            "44444444-4444-4444-4444-444444444444",
            envelope.GetProperty("executionContext").GetProperty("projectId").GetString());
    }

    [Fact]
    public void MCP002_Ingestion_tool_mapping_is_rejected_because_no_application_port_consumes_it()
    {
        var provider = CreateProfile(
            [MemoryCapabilityIds.IngestionSnapshot],
            (McpMemoryProviderConfigurationKeys.IngestionTool, JsonString("memory_ingest")));

        var exception = Assert.Throws<InvalidOperationException>(
            () => McpMemoryProviderConfiguration.FromProfile(provider, new McpMemoryProviderOptions()));

        Assert.Contains("not supported by the application runtime", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MCP003_Async_status_tool_returns_operation_result()
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

        var result = await ((IMemoryProviderOperationStatusDriver)driver).PollOperationAsync(
            provider,
            operation,
            CancellationToken.None);

        Assert.Equal(MemoryProviderOperationPollResultKind.OperationResult, result.Kind);
        Assert.Equal(MemoryOperationStatus.Succeeded, result.OperationResult?.Status);
        var call = Assert.Single(factory.LastClient!.ToolCalls);
        Assert.Equal(McpToolName.Create("memory_operation_status"), call.ToolName);
        using var arguments = JsonDocument.Parse(call.JsonArguments);
        var root = arguments.RootElement;
        Assert.Equal(operation.OperationId.Value.ToString("D"), root.GetProperty("operationId").GetString());
        Assert.Equal(operation.CorrelationId.Value.ToString("D"), root.GetProperty("correlationId").GetString());
        Assert.Equal(operation.CausationId.Value.ToString("D"), root.GetProperty("causationId").GetString());
        Assert.Equal(operation.ProviderInstanceId.Value, root.GetProperty("providerInstanceId").GetString());
        Assert.Equal(operation.RequestedCapability.Value, root.GetProperty("capabilityId").GetString());
        Assert.Equal(operation.CorrelationId.Value.ToString("D"), factory.LastCorrelationId);
        var envelope = root.GetProperty("envelope");
        Assert.Equal(
            operation.OperationId.Value.ToString("D"),
            envelope.GetProperty("operationId").GetProperty("value").GetString());
        Assert.Equal(
            "workspace-status",
            envelope.GetProperty("workspaceContext").GetProperty("workspaceId").GetString());
        Assert.Equal(
            "project-status",
            envelope.GetProperty("executionContext").GetProperty("projectId").GetString());
        Assert.Equal(
            (int)MemorySensitivity.Confidential,
            envelope.GetProperty("policyContext").GetProperty("sensitivity").GetInt32());
        Assert.Equal(
            "agent-alpha",
            envelope.GetProperty("requestedBy").GetProperty("agentId").GetString());
    }

    [Fact]
    public void MCP004_Event_polling_mapping_is_rejected_until_delivery_is_complete()
    {
        var provider = CreateProfile(
            [MemoryCapabilityIds.EventsHostPoll],
            (McpMemoryProviderConfigurationKeys.EventPollTool, JsonString("memory_poll_events")));

        var exception = Assert.Throws<InvalidOperationException>(
            () => McpMemoryProviderConfiguration.FromProfile(provider, new McpMemoryProviderOptions()));

        Assert.Contains("not supported by the application runtime", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task MCP005_Missing_query_tool_maps_unsupported_capability_without_mcp_call()
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
    public void MCP006_Manifest_mapper_declares_effective_mcp_capability_versions()
    {
        var toolMap = new McpMemoryProviderToolMap(
            ContextQueryTool: McpToolName.Create("memory_context_query"),
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
        Assert.DoesNotContain(
            manifest.Capabilities,
            capability => capability.Id == MemoryCapabilityIds.EventsHostPoll);
        Assert.DoesNotContain(manifest.Capabilities, capability => capability.Id == MemoryCapabilityIds.IngestionSnapshot);
        Assert.True(manifest.InteractionSupport.SupportsSynchronousQueries);
        Assert.True(manifest.InteractionSupport.SupportsAsynchronousOperations);
        Assert.False(manifest.InteractionSupport.SupportsProviderEvents);
        Assert.False(manifest.InteractionSupport.SupportsSourceRequests);
        Assert.False(manifest.InteractionSupport.SupportsFeedback);
        Assert.DoesNotContain(
            manifest.Capabilities,
            capability => capability.Id == MemoryCapabilityIds.IngestionProviderRequestedSource ||
                capability.Id == MemoryCapabilityIds.FeedbackImmediate ||
                capability.Id == MemoryCapabilityIds.FeedbackDelayed);
    }

    [Fact]
    public void MCP007_Internal_hosted_profile_is_rejected_before_execution()
    {
        var provider = CreateProfile(
            [MemoryCapabilityIds.ContextQuerySync],
            (McpMemoryProviderConfigurationKeys.DescriptorKind, JsonString(McpMemoryProviderDescriptorKinds.InternalHosted)));

        var exception = Assert.Throws<InvalidOperationException>(
            () => McpMemoryProviderConfiguration.FromProfile(provider, new McpMemoryProviderOptions()));

        Assert.Contains("not executable", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(McpMemoryProviderConfigurationKeys.SourceRequestTool)]
    [InlineData(McpMemoryProviderConfigurationKeys.FeedbackTool)]
    public void MCP008_Unimplemented_tool_mapping_is_rejected(string configurationKey)
    {
        var provider = CreateProfile(
            [MemoryCapabilityIds.ContextQuerySync],
            (configurationKey, JsonString("memory_unimplemented")));

        var exception = Assert.Throws<InvalidOperationException>(
            () => McpMemoryProviderConfiguration.FromProfile(provider, new McpMemoryProviderOptions()));

        Assert.Contains("not supported by the application runtime", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(McpMemoryProviderConfigurationKeys.AuthHeaderName, "Authorization\r\nInjected")]
    [InlineData(McpMemoryProviderConfigurationKeys.AuthHeaderEnvironmentVariable, "invalid-variable-name")]
    public void MCP009_Invalid_header_binding_identifier_is_rejected(
        string configurationKey,
        string invalidValue)
    {
        var provider = CreateProfile(
            [MemoryCapabilityIds.ContextQuerySync],
            (configurationKey, JsonString(invalidValue)));

        var exception = Assert.Throws<InvalidOperationException>(
            () => McpMemoryProviderConfiguration.FromProfile(provider, new McpMemoryProviderOptions()));

        Assert.Contains(configurationKey, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void MCP010_Driver_registrations_are_scoped_with_the_runtime_client_factory()
    {
        var services = new ServiceCollection();

        services.AddMcpMemoryProviderDriver();

        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, item => item.ServiceType == typeof(McpMemoryProviderDriver)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, item => item.ServiceType == typeof(IMemoryProviderDriver)).Lifetime);
        Assert.Equal(
            ServiceLifetime.Scoped,
            Assert.Single(services, item => item.ServiceType == typeof(IMemoryProviderOperationStatusDriver)).Lifetime);
        Assert.DoesNotContain(services, item => item.ServiceType == typeof(IMemoryProviderEventPollDriver));
    }

    [Fact]
    public async Task MCP011_Status_without_persisted_request_context_fails_without_provider_call()
    {
        var factory = new RecordingMcpClientFactory(new Dictionary<McpToolName, string>());
        var driver = new McpMemoryProviderDriver(factory, new McpMemoryProviderOptions());
        var provider = CreateProfile(
            [MemoryCapabilityIds.ContextQueryAsync],
            (McpMemoryProviderConfigurationKeys.OperationStatusTool, JsonString("memory_operation_status")));

        var result = await ((IMemoryProviderOperationStatusDriver)driver).PollOperationAsync(
            provider,
            CreateOperation(MemoryCapabilityIds.ContextQueryAsync, includeContext: false),
            CancellationToken.None);

        Assert.Equal(MemoryProviderOperationPollResultKind.TerminalFailure, result.Kind);
        Assert.Equal(0, factory.CreateCount);
        Assert.Contains("persisted request context", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public void MCP012_Secret_bearing_endpoint_query_is_rejected()
    {
        var provider = CreateProfile(
            [MemoryCapabilityIds.ContextQuerySync],
            (McpMemoryProviderConfigurationKeys.RemoteEndpoint, JsonString("https://memory-mcp.test/mcp?token=secret")),
            (McpMemoryProviderConfigurationKeys.ContextQueryTool, JsonString("memory_context_query")));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            McpMemoryProviderConfiguration.FromProfile(provider, new McpMemoryProviderOptions()));

        Assert.Contains("query strings", exception.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", exception.Message, StringComparison.Ordinal);
    }

    private static MemoryProviderProfile CreateProfile(
        IReadOnlyList<MemoryCapabilityId> capabilities,
        params (string Key, JsonElement Value)[] extensions)
    {
        var values = new Dictionary<string, JsonElement>(StringComparer.Ordinal)
        {
            [McpMemoryProviderConfigurationKeys.DescriptorKind] = JsonString(McpMemoryProviderDescriptorKinds.RemoteHttp),
            [McpMemoryProviderConfigurationKeys.ServerKey] = JsonString("memory-mcp"),
            [McpMemoryProviderConfigurationKeys.RemoteEndpoint] = JsonString("https://memory-mcp.test/mcp"),
            [McpMemoryProviderConfigurationKeys.DisplayName] = JsonString("Memory MCP"),
            [McpMemoryProviderConfigurationKeys.Description] = JsonString("Test MCP memory provider"),
            [McpMemoryProviderConfigurationKeys.AuthHeaderName] = JsonString("Authorization"),
            [McpMemoryProviderConfigurationKeys.AuthHeaderEnvironmentVariable] = JsonString("CANDOITALL_MEMORY_MCP_TEST_KEY")
        };
        foreach (var (key, value) in extensions)
        {
            values[key] = value;
        }

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
                new MemoryExtensionData(values)));
    }

    private static MemoryOperationRecord CreateOperation(
        MemoryCapabilityId capability,
        bool includeContext = true)
    {
        var now = new DateTimeOffset(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);
        var operation = MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            new MemoryOperationId(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            MemoryProviderInstanceId.Parse("mcp-provider"),
            capability,
            MemoryOperationKind.ContextQuery,
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
        if (!includeContext)
        {
            return operation;
        }

        var context = new MemoryRequestContext(
            new MemoryWorkspaceContext(
                "workspace-status",
                "Status workspace",
                CustomerId: null,
                Domain: "engineering",
                Tags: ["memory"]),
            new MemoryExecutionContext(
                "project-status",
                "Status project",
                "process-alpha",
                "step-alpha",
                "Status step",
                "workflow-alpha",
                "node-alpha",
                ["artifact-alpha"]),
            MemoryPolicyContext.InternalDefault with
            {
                Sensitivity = MemorySensitivity.Confidential
            },
            new MemoryBudget(5, 4_096, 1_024, TimeSpan.FromSeconds(5)),
            MemoryExtensionData.Empty);
        return operation with
        {
            Extensions = operation.Extensions.WithMemoryRequestContext(operation, context)
        };
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

        public string? LastCorrelationId { get; private set; }

        public Task<IMcpRuntimeClient> CreateAsync(
            McpServerDescriptor descriptor,
            string correlationId,
            CancellationToken cancellationToken)
        {
            CreateCount++;
            LastCorrelationId = correlationId;
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
