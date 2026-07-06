using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class MemoryAgentRuntimeToolProviderTests
{
    private static readonly JsonSerializerOptions FunctionResultJsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task CreateToolsAsync_returns_memory_tools_and_metadata_when_agent_is_allowed()
    {
        var handler = new RecordingMemoryOperationHandler();
        var toolProvider = new MemoryAgentRuntimeToolProvider(handler, TimeProvider.System);
        var agent = CreateAgent(new AgentMemoryAccessSettings
        {
            CanUseMemoryTools = true,
            CanIngestSources = true,
            PreferredProviderInstanceId = "memory.programming",
            AllowedSourceScopes = [MemorySourceScope.Manual]
        });

        var context = CreateContext(agent);
        var tools = await toolProvider.CreateToolsAsync(context, CancellationToken.None);
        var metadata = toolProvider.GetToolMetadata(context);

        Assert.Equal(6, tools.Count);
        Assert.Equal(925, toolProvider.Order);
        Assert.Equal(MemoryAgentRuntimeToolProvider.ProviderKey, toolProvider.Descriptor?.ProviderKey);
        Assert.Contains(tools, tool => tool.Name == MemoryAgentRuntimeToolNames.ContextQuery);
        Assert.Contains(tools, tool => tool.Name == MemoryAgentRuntimeToolNames.IngestText);
        Assert.Contains(tools, tool => tool.Name == MemoryAgentRuntimeToolNames.FeedbackSubmit);
        Assert.Contains(tools, tool => tool.Name == MemoryAgentRuntimeToolNames.OperationStatus);
        Assert.Contains(tools, tool => tool.Name == MemoryAgentRuntimeToolNames.OperationCancel);
        Assert.Contains(tools, tool => tool.Name == MemoryAgentRuntimeToolNames.EventAcknowledge);
        Assert.Contains(metadata, item => item.ToolName == MemoryAgentRuntimeToolNames.ContextQuery && item.OperationKind == AgentRuntimeToolOperationKind.Read);
        Assert.Contains(metadata, item => item.ToolName == MemoryAgentRuntimeToolNames.OperationCancel && item.OperationKind == AgentRuntimeToolOperationKind.Mutation);
    }

    [Fact]
    public async Task Context_query_tool_routes_to_handler_and_shapes_context_result()
    {
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.CompletedQuery("Architecture notes", "Use generic memory provider boundaries.")
        };
        var toolProvider = new MemoryAgentRuntimeToolProvider(handler, TimeProvider.System);
        var agent = CreateAgent(new AgentMemoryAccessSettings
        {
            CanUseMemoryTools = true,
            PreferredProviderInstanceId = "memory.programming",
            AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync]
        });

        var tool = await CreateToolAsync(toolProvider, agent, MemoryAgentRuntimeToolNames.ContextQuery);
        var result = await InvokeToolAsync<MemoryContextQueryToolResult>(
            tool,
            new MemoryContextQueryToolInput("How should memory providers be isolated?"));

        Assert.Equal(MemoryToolResultStatus.Completed, result.Status);
        Assert.Equal("memory.programming", result.ProviderInstanceId);
        Assert.Equal("Architecture notes", result.Summary);
        Assert.Equal("Use generic memory provider boundaries.", Assert.Single(result.Sections).Text);
        Assert.NotNull(result.FeedbackHandle);
        Assert.NotNull(handler.LastQuery);
        Assert.Equal("memory.programming", handler.LastQuery.SelectionPolicy.ExplicitProviderId?.Value);
        Assert.Equal(MemoryOperationCallerKind.Tool, handler.LastQuery.Caller.Kind);
        Assert.Equal(agent.Id.ToString("D"), handler.LastQuery.Caller.Requester.AgentId);
    }

    [Fact]
    public async Task Different_agents_select_different_memory_providers_in_same_process()
    {
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.CompletedQuery("Programming context", "Provider selected.")
        };
        var toolProvider = new MemoryAgentRuntimeToolProvider(handler, TimeProvider.System);
        var programmingAgent = CreateAgent(new AgentMemoryAccessSettings
        {
            CanUseMemoryTools = true,
            PreferredProviderInstanceId = "memory.programming"
        });
        var businessAgent = CreateAgent(new AgentMemoryAccessSettings
        {
            CanUseMemoryTools = true,
            PreferredProviderInstanceId = "memory.business"
        });

        var programmingTool = await CreateToolAsync(toolProvider, programmingAgent, MemoryAgentRuntimeToolNames.ContextQuery);
        var businessTool = await CreateToolAsync(toolProvider, businessAgent, MemoryAgentRuntimeToolNames.ContextQuery);

        await InvokeToolAsync<MemoryContextQueryToolResult>(
            programmingTool,
            new MemoryContextQueryToolInput("programming query"));
        await InvokeToolAsync<MemoryContextQueryToolResult>(
            businessTool,
            new MemoryContextQueryToolInput("business query"));

        Assert.Equal(
            ["memory.programming", "memory.business"],
            handler.QueryRequests.Select(request => request.SelectionPolicy.ExplicitProviderId?.Value ?? string.Empty).ToArray());
    }

    [Fact]
    public async Task Context_query_tool_returns_typed_result_for_no_provider_and_unsupported_capability()
    {
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.NoProviderQuery()
        };
        var toolProvider = new MemoryAgentRuntimeToolProvider(handler, TimeProvider.System);
        var agent = CreateAgent(new AgentMemoryAccessSettings
        {
            CanUseMemoryTools = true,
            PreferredProviderInstanceId = "memory.missing"
        });

        var tool = await CreateToolAsync(toolProvider, agent, MemoryAgentRuntimeToolNames.ContextQuery);
        var noProvider = await InvokeToolAsync<MemoryContextQueryToolResult>(
            tool,
            new MemoryContextQueryToolInput("query"));
        handler.QueryResult = RecordingMemoryOperationHandler.UnsupportedCapabilityQuery();
        var unsupportedCapability = await InvokeToolAsync<MemoryContextQueryToolResult>(
            tool,
            new MemoryContextQueryToolInput("query"));

        Assert.Equal(MemoryToolResultStatus.NoProviderConfigured, noProvider.Status);
        Assert.False(noProvider.DispatchAttempted);
        Assert.Equal(MemoryToolResultStatus.CapabilityUnavailable, unsupportedCapability.Status);
        Assert.False(unsupportedCapability.DispatchAttempted);
    }

    [Fact]
    public async Task Async_accepted_query_result_includes_status_path()
    {
        var operationId = MemoryOperationId.New();
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.AcceptedQuery(operationId)
        };
        var toolProvider = new MemoryAgentRuntimeToolProvider(handler, TimeProvider.System);
        var agent = CreateAgent(new AgentMemoryAccessSettings
        {
            CanUseMemoryTools = true,
            PreferredProviderInstanceId = "memory.async",
            AllowedCapabilityIds = [MemoryCapabilityIds.ContextQueryAsync]
        });

        var tool = await CreateToolAsync(toolProvider, agent, MemoryAgentRuntimeToolNames.ContextQuery);
        var result = await InvokeToolAsync<MemoryContextQueryToolResult>(
            tool,
            new MemoryContextQueryToolInput("async query", AllowAsync: true));

        Assert.Equal(MemoryToolResultStatus.Accepted, result.Status);
        Assert.NotNull(result.AsyncOperation);
        Assert.Equal(operationId.Value, result.AsyncOperation.OperationId);
        Assert.Equal("/memory/operations/" + operationId, result.AsyncOperation.StatusPath);
        Assert.Equal(MemoryCapabilityIds.ContextQueryAsync.Value, handler.LastQuery?.SelectionPolicy.RequiredCapability.Value);
    }

    [Fact]
    public async Task Ingest_text_tool_denies_manual_source_scope_before_dispatch()
    {
        var handler = new RecordingMemoryOperationHandler();
        var toolProvider = new MemoryAgentRuntimeToolProvider(handler, TimeProvider.System);
        var agent = CreateAgent(new AgentMemoryAccessSettings
        {
            CanUseMemoryTools = true,
            CanIngestSources = true,
            PreferredProviderInstanceId = "memory.programming",
            AllowedSourceScopes = [MemorySourceScope.Project]
        });

        var tool = await CreateToolAsync(toolProvider, agent, MemoryAgentRuntimeToolNames.IngestText);
        var result = await InvokeToolAsync<MemoryIngestTextToolResult>(
            tool,
            new MemoryIngestTextToolInput(
                "Manual note",
                "Provider extraction notes.",
                SourceCategory: "architecture"));

        Assert.Equal(MemoryToolResultStatus.SourceScopeDenied, result.Status);
        Assert.False(result.DispatchAttempted);
        Assert.Empty(handler.SourceCaptureRequests);
    }

    [Fact]
    public void AddAgentFrameworkModule_registers_generic_memory_runtime_tool_provider()
    {
        var services = new ServiceCollection();
        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IAgentRuntimeToolProvider) &&
                          descriptor.ImplementationType == typeof(MemoryAgentRuntimeToolProvider) &&
                          descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    [Fact]
    public void AgentMemoryAccessMetadata_round_trips_provider_policy_settings()
    {
        var configurationJson = AgentMemoryAccessMetadata.Write(
            """{"existing":true}""",
            new AgentMemoryAccessSettings
            {
                CanUseMemoryTools = true,
                CanIngestSources = true,
                PreferredProviderInstanceId = " memory.programming ",
                DefaultProviderInstanceId = "memory.default",
                AllowedProviderInstanceIds = ["memory.programming", " memory.business "],
                AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync, MemoryCapabilityIds.FeedbackImmediate],
                DeniedCapabilityIds = [MemoryCapabilityIds.ContextQueryAsync],
                AllowedSourceScopes = [MemorySourceScope.Manual, MemorySourceScope.Project],
                ProviderAssignments =
                [
                    new AgentMemoryProviderAssignmentSetting(
                        MemoryProviderAssignmentScope.Workflow,
                        " workflow-a ",
                        " memory.business ")
                ]
            });

        var settings = AgentMemoryAccessMetadata.Read(configurationJson);

        Assert.True(settings.CanUseMemoryTools);
        Assert.True(settings.CanIngestSources);
        Assert.Equal("memory.programming", settings.PreferredProviderInstanceId);
        Assert.Equal("memory.default", settings.DefaultProviderInstanceId);
        Assert.Equal(["memory.programming", "memory.business"], settings.AllowedProviderInstanceIds);
        Assert.Equal(
            [MemoryCapabilityIds.ContextQuerySync, MemoryCapabilityIds.FeedbackImmediate],
            settings.AllowedCapabilityIds);
        Assert.Equal([MemoryCapabilityIds.ContextQueryAsync], settings.DeniedCapabilityIds);
        Assert.Equal([MemorySourceScope.Manual, MemorySourceScope.Project], settings.AllowedSourceScopes);
        Assert.Equal(MemoryProviderAssignmentScope.Workflow, Assert.Single(settings.ProviderAssignments).Scope);
        Assert.Contains("\"existing\":true", configurationJson, StringComparison.Ordinal);
    }

    private static async Task<AITool> CreateToolAsync(
        MemoryAgentRuntimeToolProvider toolProvider,
        AgentDefinition agent,
        string toolName)
    {
        var tools = await toolProvider.CreateToolsAsync(CreateContext(agent), CancellationToken.None);
        return tools.Single(tool => tool.Name == toolName);
    }

    private static async Task<TResult> InvokeToolAsync<TResult>(
        AITool tool,
        object input)
    {
        var function = Assert.IsAssignableFrom<AIFunction>(tool);
        var rawResult = await function.InvokeAsync(
            new AIFunctionArguments
            {
                ["request"] = input
            });

        return rawResult switch
        {
            TResult result => result,
            JsonElement jsonElement => JsonSerializer.Deserialize<TResult>(jsonElement.GetRawText(), FunctionResultJsonOptions)
                ?? throw new InvalidOperationException("Memory function returned null JSON."),
            _ => throw new InvalidOperationException($"Unexpected memory tool result type '{rawResult?.GetType().FullName ?? "<null>"}'.")
        };
    }

    private static AgentRuntimeToolProviderContext CreateContext(AgentDefinition agent)
    {
        var provider = new ProviderProfile(
            Guid.NewGuid(),
            "OpenAI chat",
            ProviderKind.OpenAi,
            "https://api.openai.com",
            "OPENAI_API_KEY",
            "gpt-5-mini",
            ProviderTransportKind.ChatCompletions,
            IsEnabled: true,
            SupportsStreaming: true,
            SupportsTools: true,
            PreferFrameworkManagedChatHistory: true,
            SupportsBackgroundResponses: false,
            ConfigurationJson: string.Empty,
            Notes: string.Empty,
            HealthStatus: string.Empty,
            LastCheckedAtUtc: null,
            SuggestedModels: [],
            ProviderProfilePurpose.Chat);

        return new AgentRuntimeToolProviderContext(
            agent,
            provider,
            [],
            SuppressApprovalRequirements: false,
            AgentRuntimeToolProviderPurpose.InteractiveChat,
            RuntimeSessionKey: "unit-memory-tools",
            AgentRuntimeContextIntent.Empty,
            Tags: new Dictionary<string, string>
            {
                [MemoryAgentRuntimeToolTags.WorkflowId] = "workflow-a",
                [MemoryAgentRuntimeToolTags.ProcessId] = "process-a"
            });
    }

    private static AgentDefinition CreateAgent(AgentMemoryAccessSettings memoryAccess)
    {
        var now = DateTimeOffset.UtcNow;
        return new AgentDefinition(
            Guid.NewGuid(),
            "Memory test agent",
            "Memory tester",
            "Tests generic memory tool provider.",
            "Use memory deliberately.",
            AgentLifecycleStatus.Active,
            Guid.NewGuid(),
            "gpt-5-mini",
            AgentWorkloadKind.Programming,
            AgentChatHistoryMode.ProviderDefault,
            0.2,
            RequirePerServiceCallChatHistoryPersistence: false,
            EnableBackgroundResponses: false,
            AgentMemoryAccessMetadata.Write("{}", memoryAccess),
            IsTemplate: false,
            TemplateKey: string.Empty,
            AgentPermissionsPolicy.Default,
            [],
            [],
            now,
            now);
    }

    private sealed class RecordingMemoryOperationHandler : IMemoryOperationHandler
    {
        private static readonly MemoryProviderProfile ProgrammingProvider = CreateProvider("memory.programming");

        public MemoryOperationHandlerResult<MemoryContextPack> QueryResult { get; set; } =
            CompletedQuery("Context", "Result");

        public List<MemoryOperationHandlerRequest<MemoryContextQueryRequest>> QueryRequests { get; } = [];

        public List<MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest>> SourceCaptureRequests { get; } = [];

        public MemoryOperationHandlerRequest<MemoryContextQueryRequest>? LastQuery => QueryRequests.LastOrDefault();

        public static MemoryOperationHandlerResult<MemoryContextPack> CompletedQuery(
            string summary,
            string sectionText)
        {
            var contextPack = new MemoryContextPack(
                MemoryContextPackId.New(),
                summary,
                [
                    new MemoryContextSection(
                        "Decision",
                        sectionText,
                        [new MemoryCitation("source:architecture", "Architecture")],
                        0.9m)
                ],
                [new MemoryWarning(MemoryWarningKind.ProviderPartial, "Provider returned partial context.")],
                0.88m,
                MemoryFeedbackHandle.Parse("feedback-" + Guid.NewGuid().ToString("N")));
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.Completed,
                MemoryProviderSelectionResult.Selected(ProgrammingProvider, MemoryProviderSelectionReason.ExplicitProvider, MemoryCapabilityIds.ContextQuerySync),
                OperationRecord: null,
                contextPack,
                AcceptedOperation: null,
                contextPack.FeedbackHandle,
                DriverDispatchAttempted: true,
                Diagnostic: "Context query completed.");
        }

        public static MemoryOperationHandlerResult<MemoryContextPack> NoProviderQuery()
        {
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.NoProviderConfigured,
                MemoryProviderSelectionResult.Rejected(
                    MemoryProviderSelectionStatus.NoProviderConfigured,
                    MemoryProviderSelectionReason.None,
                    MemoryCapabilityIds.ContextQuerySync,
                    "No enabled memory provider is configured.",
                    []),
                OperationRecord: null,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                Diagnostic: "No enabled memory provider is configured.");
        }

        public static MemoryOperationHandlerResult<MemoryContextPack> UnsupportedCapabilityQuery()
        {
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.CapabilityUnavailable,
                MemoryProviderSelectionResult.Rejected(
                    MemoryProviderSelectionStatus.CapabilityUnavailable,
                    MemoryProviderSelectionReason.ExplicitProvider,
                    MemoryCapabilityIds.ContextQuerySync,
                    "Selected memory provider does not support synchronous context query.",
                    [ProgrammingProvider.InstanceId]),
                OperationRecord: null,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                Diagnostic: "Selected memory provider does not support synchronous context query.");
        }

        public static MemoryOperationHandlerResult<MemoryContextPack> AcceptedQuery(MemoryOperationId operationId)
        {
            var accepted = new MemoryOperationAccepted(
                operationId,
                "/memory/operations/" + operationId,
                DateTimeOffset.UtcNow.AddMinutes(30),
                TimeSpan.FromSeconds(3),
                CallbackAvailable: false);
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.Accepted,
                MemoryProviderSelectionResult.Selected(
                    CreateProvider("memory.async"),
                    MemoryProviderSelectionReason.ExplicitProvider,
                    MemoryCapabilityIds.ContextQueryAsync),
                OperationRecord: null,
                Output: null,
                accepted,
                FeedbackHandle: null,
                DriverDispatchAttempted: true,
                Diagnostic: "Context query accepted.");
        }

        public Task<MemoryOperationHandlerResult<MemoryContextPack>> ExecuteQueryAsync(
            MemoryOperationHandlerRequest<MemoryContextQueryRequest> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            QueryRequests.Add(request);
            return Task.FromResult(QueryResult);
        }

        public Task<MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>> CaptureSourceForIngestionAsync(
            MemoryOperationHandlerRequest<MemorySourceCaptureOperationRequest> request,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            SourceCaptureRequests.Add(request);
            return Task.FromResult(new MemoryOperationHandlerResult<MemorySourceCaptureOperationResult>(
                MemoryOperationHandlerStatus.Accepted,
                MemoryProviderSelectionResult.Selected(
                    ProgrammingProvider,
                    MemoryProviderSelectionReason.ExplicitProvider,
                    MemoryCapabilityIds.IngestionSnapshot),
                OperationRecord: null,
                new MemorySourceCaptureOperationResult(
                    new MemorySourceIngestionJobRecord(
                        Guid.NewGuid(),
                        ProgrammingProvider.InstanceId,
                        request.Payload.SourceGatewayRequest,
                        MemorySourceIngestionJobStatus.SnapshotCaptured,
                        DateTimeOffset.UtcNow,
                        DateTimeOffset.UtcNow,
                        "captured"),
                    [MemorySourcePayloadForm.TextSection]),
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                Diagnostic: "Source capture accepted."));
        }

        public Task<MemoryOperationHandlerResult<MemoryFeedbackRecord>> SubmitFeedbackAsync(
            MemoryOperationHandlerRequest<MemoryFeedbackOperationRequest> request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> GetStatusAsync(
            MemoryOperationHandlerRequest<MemoryOperationStatusRequest> request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryOperationRecord>> CancelAsync(
            MemoryOperationHandlerRequest<MemoryOperationCancellationRequest> request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<MemoryOperationHandlerResult<MemoryEventOutboxRecord>> AcknowledgeEventAsync(
            MemoryOperationHandlerRequest<MemoryEventAcknowledgeRequest> request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        private static MemoryProviderProfile CreateProvider(string providerId)
        {
            return new MemoryProviderProfile(
                MemoryProviderInstanceId.Parse(providerId),
                providerId,
                MemoryProviderDriverKind.Mock,
                IsEnabled: true,
                MemoryProviderHealthState.Healthy,
                MemoryProviderWorkspaceScope.AllWorkspaces,
                SelectionTags: [],
                MemoryProviderProfilePolicy.Default,
                new MemoryProviderManifest(
                    MemoryProviderKind.Parse("mock.memory"),
                    MemoryProtocolVersion.Current,
                    [
                        new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, "v1", Supported: true),
                        new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQueryAsync, "v1", Supported: true),
                        new MemoryCapabilityDescriptor(MemoryCapabilityIds.IngestionSnapshot, "v1", Supported: true)
                    ],
                    MemoryProviderInteractionSupport.SyncQueryOnly,
                    UiSurfaces: [],
                    MemoryProviderLimits.Default,
                    MemoryExtensionData.Empty));
        }
    }
}
