using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Unit;

public sealed class MemoryWorkflowExecutorTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Descriptor_source_lists_generic_memory_executor_and_legacy_mapping()
    {
        var descriptors = new MemoryWorkflowExecutorDescriptorSource().ListExecutorDescriptors().ToArray();

        var descriptor = Assert.Single(descriptors);
        Assert.Equal(WorkflowExecutorIds.Memory, descriptor.Id);
        Assert.True(descriptor.CanExecute);
        Assert.True(MemoryWorkflowExecutorCompatibility.TryMapLegacyExecutorId(new WorkflowExecutorId("cognitive-memory.recall"), out var mappedId));
        Assert.Equal(WorkflowExecutorIds.Memory, mappedId);
    }

    [Fact]
    public async Task Context_query_executes_through_shared_handler_and_shapes_result()
    {
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.CompletedQuery("Workflow context", "Use the generic handler.")
        };
        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);

        var result = await ExecuteAsync<MemoryContextQueryToolResult>(
            executor,
            new MemoryWorkflowExecutorSettings
            {
                Operation = MemoryWorkflowOperation.ContextQuery,
                Query = "How should workflow memory execute?",
                ProviderInstanceId = "memory.workflow",
                AllowedCapabilityIds = [MemoryCapabilityIds.ContextQuerySync.Value]
            });

        Assert.Equal(MemoryToolResultStatus.Completed, result.Status);
        Assert.Equal("Workflow context", result.Summary);
        Assert.Equal("Use the generic handler.", Assert.Single(result.Sections).Text);
        Assert.NotNull(handler.LastQuery);
        Assert.Equal("memory.workflow", handler.LastQuery.SelectionPolicy.ExplicitProviderId?.Value);
        Assert.Equal(MemoryOperationCallerKind.WorkflowExecutor, handler.LastQuery.Caller.Kind);
        Assert.Equal("workflow-memory", handler.LastQuery.Caller.Route);
    }

    [Fact]
    public async Task Context_query_uses_input_query_when_settings_query_is_empty()
    {
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.CompletedQuery("Input context", "Input query used.")
        };
        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);

        await ExecuteAsync<MemoryContextQueryToolResult>(
            executor,
            new MemoryWorkflowExecutorSettings
            {
                Operation = MemoryWorkflowOperation.ContextQuery,
                ProviderInstanceId = "memory.workflow"
            },
            """{"query":"Use workflow input"}""");

        Assert.Equal("Use workflow input", handler.LastQuery?.Payload.Query);
    }

    [Fact]
    public async Task Async_context_query_returns_accepted_status_without_unbounded_wait()
    {
        var operationId = MemoryOperationId.New();
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.AcceptedQuery(operationId)
        };
        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);

        var result = await ExecuteAsync<MemoryContextQueryToolResult>(
            executor,
            new MemoryWorkflowExecutorSettings
            {
                Operation = MemoryWorkflowOperation.ContextQuery,
                Query = "async workflow query",
                ProviderInstanceId = "memory.async",
                AllowAsync = true,
                WaitForAsyncCompletion = false,
                AllowedCapabilityIds = [MemoryCapabilityIds.ContextQueryAsync.Value]
            });

        Assert.Equal(MemoryToolResultStatus.Accepted, result.Status);
        Assert.NotNull(result.AsyncOperation);
        Assert.Equal(operationId.Value, result.AsyncOperation.OperationId);
        Assert.Equal(MemoryCapabilityIds.ContextQueryAsync.Value, handler.LastQuery?.SelectionPolicy.RequiredCapability.Value);
    }

    [Fact]
    public async Task No_provider_result_is_typed_and_does_not_hide_fallback()
    {
        var handler = new RecordingMemoryOperationHandler
        {
            QueryResult = RecordingMemoryOperationHandler.NoProviderQuery()
        };
        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);

        var result = await ExecuteAsync<MemoryContextQueryToolResult>(
            executor,
            new MemoryWorkflowExecutorSettings
            {
                Operation = MemoryWorkflowOperation.ContextQuery,
                Query = "query",
                ProviderInstanceId = "memory.missing"
            });

        Assert.Equal(MemoryToolResultStatus.NoProviderConfigured, result.Status);
        Assert.False(result.DispatchAttempted);
    }

    [Fact]
    public async Task Capability_policy_denies_query_before_handler_dispatch()
    {
        var handler = new RecordingMemoryOperationHandler();
        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);

        var result = await ExecuteAsync<MemoryContextQueryToolResult>(
            executor,
            new MemoryWorkflowExecutorSettings
            {
                Operation = MemoryWorkflowOperation.ContextQuery,
                Query = "query",
                ProviderInstanceId = "memory.workflow",
                AllowedCapabilityIds = [MemoryCapabilityIds.FeedbackImmediate.Value]
            });

        Assert.Equal(MemoryToolResultStatus.CapabilityDenied, result.Status);
        Assert.Empty(handler.QueryRequests);
    }

    [Fact]
    public async Task Manual_ingestion_denies_source_scope_before_handler_dispatch()
    {
        var handler = new RecordingMemoryOperationHandler();
        var executor = new MemoryWorkflowExecutor(handler, TimeProvider.System);

        var result = await ExecuteAsync<MemoryIngestTextToolResult>(
            executor,
            new MemoryWorkflowExecutorSettings
            {
                Operation = MemoryWorkflowOperation.IngestText,
                ProviderInstanceId = "memory.workflow",
                Title = "Workflow note",
                ContentText = "Memory source content.",
                SourceCategory = "workflow",
                AllowedSourceScopes = [MemorySourceScope.Project.ToString()]
            });

        Assert.Equal(MemoryToolResultStatus.SourceScopeDenied, result.Status);
        Assert.False(result.DispatchAttempted);
        Assert.Empty(handler.SourceCaptureRequests);
    }

    [Fact]
    public void AddAgentFrameworkModule_registers_generic_memory_workflow_executor()
    {
        var services = new ServiceCollection();
        services.AddAgentFrameworkModule(new ConfigurationBuilder().Build());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IWorkflowExecutor) &&
                          descriptor.ImplementationType == typeof(MemoryWorkflowExecutor) &&
                          descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorDescriptorSource) &&
                          descriptor.ImplementationType == typeof(MemoryWorkflowExecutorDescriptorSource) &&
                          descriptor.Lifetime == ServiceLifetime.Singleton);
    }

    private static async Task<TResult> ExecuteAsync<TResult>(
        MemoryWorkflowExecutor executor,
        MemoryWorkflowExecutorSettings settings,
        string inputJson = "{}")
    {
        var result = await executor.ExecuteAsync(
            CreateContext(executor.Descriptor, settings),
            new WorkflowNodeInput(inputJson));
        return JsonSerializer.Deserialize<TResult>(result.PayloadJson, JsonOptions)
            ?? throw new InvalidOperationException("Memory workflow executor returned null JSON.");
    }

    private static WorkflowExecutorExecutionContext CreateContext(
        WorkflowExecutorDescriptor descriptor,
        MemoryWorkflowExecutorSettings settings)
    {
        var node = new WorkflowNode(
            new WorkflowNodeId("workflow-memory"),
            WorkflowNodeKind.Executor,
            "workflow-memory",
            [],
            new WorkflowNodeSettings(
                ComponentId: null,
                AgentId: null,
                SubworkflowId: null,
                ExternalRequestKind: null,
                Instructions: string.Empty,
                InputShape: WorkflowValueShape.Text,
                ResultShape: WorkflowExecutorDescriptorFactory.JsonShape)
            {
                ExecutorId = descriptor.Id,
                ExecutorSettingsJson = JsonSerializer.Serialize(settings, JsonOptions),
                ExecutionPolicy = WorkflowExecutorExecutionPolicy.Default
            });
        var definition = new WorkflowDefinition(
            WorkflowId.New(),
            WorkflowVersionId.New(),
            "Memory workflow",
            "Memory workflow executor tests.",
            WorkflowLifecycleStatus.Draft,
            new WorkflowGraph(node.Id, [node], []),
            new WorkflowRuntimePolicy(
                WorkflowRuntimeBackendKind.InProcess,
                AllowInProcessPreviewRuns: true,
                RequireDurableProductionRuns: false,
                ExposeAzureFunctionsStatusEndpoint: false,
                ExposeAzureFunctionsMcpTool: false),
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);

        return new WorkflowExecutorExecutionContext(
            definition,
            node,
            descriptor,
            node.Settings.ExecutorSettingsJson,
            WorkflowExecutorExecutionPolicy.Default)
        {
            RunId = WorkflowRunId.New()
        };
    }

    private sealed class RecordingMemoryOperationHandler : IMemoryOperationHandler
    {
        private static readonly MemoryProviderProfile WorkflowProvider = CreateProvider("memory.workflow");

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
                        "Workflow",
                        sectionText,
                        [new MemoryCitation("workflow:source", "Workflow source")],
                        0.91m)
                ],
                [],
                0.89m,
                MemoryFeedbackHandle.Parse("feedback-" + Guid.NewGuid().ToString("N")));
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.Completed,
                MemoryProviderSelectionResult.Selected(WorkflowProvider, MemoryProviderSelectionReason.ExplicitProvider, MemoryCapabilityIds.ContextQuerySync),
                OperationRecord: null,
                contextPack,
                AcceptedOperation: null,
                contextPack.FeedbackHandle,
                DriverDispatchAttempted: true,
                Diagnostic: "Context query completed.");
        }

        public static MemoryOperationHandlerResult<MemoryContextPack> AcceptedQuery(MemoryOperationId operationId)
        {
            var accepted = new MemoryOperationAccepted(
                operationId,
                "/memory/operations/" + operationId,
                DateTimeOffset.UtcNow.AddMinutes(30),
                TimeSpan.FromSeconds(2),
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

        public static MemoryOperationHandlerResult<MemoryContextPack> NoProviderQuery()
        {
            return new MemoryOperationHandlerResult<MemoryContextPack>(
                MemoryOperationHandlerStatus.NoProviderConfigured,
                MemoryProviderSelectionResult.Rejected(
                    MemoryProviderSelectionStatus.NoProviderConfigured,
                    MemoryProviderSelectionReason.None,
                    MemoryCapabilityIds.ContextQuerySync,
                    "No memory provider configured.",
                    []),
                OperationRecord: null,
                Output: null,
                AcceptedOperation: null,
                FeedbackHandle: null,
                DriverDispatchAttempted: false,
                Diagnostic: "No memory provider configured.");
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
            throw new NotSupportedException();
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
