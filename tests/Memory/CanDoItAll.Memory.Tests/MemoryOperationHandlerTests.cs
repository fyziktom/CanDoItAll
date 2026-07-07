using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryOperationHandlerTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T19:15:00Z");

    [Fact]
    public async Task Tool_and_workflow_executor_routes_share_handler_and_record_equivalent_lifecycle()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: true);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(CreateProviderProfile(MemoryCapabilityIds.ContextQuerySync), Now);
        var handler = provider.GetRequiredService<IMemoryOperationHandler>();
        var tool = new FakeToolMemoryRoute(handler);
        var executor = new FakeWorkflowExecutorMemoryRoute(handler);

        var toolResult = await tool.QueryAsync(CreateQueryRequest());
        var executorResult = await executor.QueryAsync(CreateQueryRequest());

        Assert.Equal(MemoryOperationHandlerStatus.Completed, toolResult.Status);
        Assert.Equal(MemoryOperationHandlerStatus.Completed, executorResult.Status);
        Assert.NotNull(toolResult.OperationRecord);
        Assert.NotNull(executorResult.OperationRecord);
        Assert.Equal(toolResult.OperationRecord.ProviderInstanceId, executorResult.OperationRecord.ProviderInstanceId);
        Assert.Equal(toolResult.OperationRecord.RequestedCapability, executorResult.OperationRecord.RequestedCapability);
        Assert.Equal(toolResult.OperationRecord.OperationKind, executorResult.OperationRecord.OperationKind);
        Assert.Equal(toolResult.OperationRecord.SourceSnapshotIds, executorResult.OperationRecord.SourceSnapshotIds);
        Assert.NotEqual(toolResult.OperationRecord.OperationId, executorResult.OperationRecord.OperationId);
        Assert.Equal(MemoryOperationCallerKind.Tool, toolResult.OperationRecord.Extensions.GetMemoryOperationCaller()?.Kind);
        Assert.Equal(MemoryOperationCallerKind.WorkflowExecutor, executorResult.OperationRecord.Extensions.GetMemoryOperationCaller()?.Kind);
        Assert.NotNull(toolResult.Output?.FeedbackHandle);
        Assert.NotNull(executorResult.Output?.FeedbackHandle);
    }

    [Theory]
    [MemberData(nameof(CrossCallerRoutes))]
    public async Task No_provider_denial_is_consistent_for_all_handler_callers(MemoryOperationCaller caller)
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: false);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var handler = provider.GetRequiredService<IMemoryOperationHandler>();

        var result = await handler.ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
            caller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync),
            CreateQueryRequest(),
            CreateRetentionPolicy()));

        Assert.Equal(MemoryOperationHandlerStatus.NoProviderConfigured, result.Status);
        Assert.Equal(MemoryProviderSelectionStatus.NoProviderConfigured, result.Selection.Status);
        Assert.False(result.DriverDispatchAttempted);
        Assert.Null(result.OperationRecord);
        Assert.Null(result.Output);
        Assert.Empty(provider.GetServices<IMemoryProviderDriver>());
    }

    [Theory]
    [MemberData(nameof(CrossCallerRoutes))]
    public async Task Capability_mismatch_denial_is_consistent_for_all_handler_callers(MemoryOperationCaller caller)
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: true);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(CreateProviderProfile(MemoryCapabilityIds.FeedbackImmediate), Now);
        var handler = provider.GetRequiredService<IMemoryOperationHandler>();

        var result = await handler.ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
            caller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync),
            CreateQueryRequest(),
            CreateRetentionPolicy()));

        Assert.Equal(MemoryOperationHandlerStatus.CapabilityUnavailable, result.Status);
        Assert.Equal(MemoryProviderSelectionStatus.CapabilityUnavailable, result.Selection.Status);
        Assert.False(result.DriverDispatchAttempted);
        Assert.Null(result.OperationRecord);
        Assert.Null(result.Output);
    }

    [Fact]
    public void Request_builders_cover_shared_operation_kinds()
    {
        var caller = MemoryOperationCaller.UiAction("memory.admin.query", CreateRequester());

        Assert.Equal(MemoryOperationKind.ContextQuery, MemoryOperationRequestBuilder.Query(
            caller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync),
            CreateQueryRequest(),
            CreateRetentionPolicy()).OperationKind);
        Assert.Equal(MemoryOperationKind.Ingestion, MemoryOperationRequestBuilder.Ingestion(
            caller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.IngestionSnapshot),
            CreateIngestionRequest(),
            CreateRetentionPolicy()).OperationKind);
        Assert.Equal(MemoryOperationKind.Feedback, MemoryOperationRequestBuilder.Feedback(
            caller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.FeedbackImmediate),
            CreateFeedbackRequest(),
            CreateRetentionPolicy()).OperationKind);
        Assert.Equal(MemoryOperationKind.OperationStatus, MemoryOperationRequestBuilder.Status(
            caller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationStatusRequest(MemoryOperationId.New()),
            CreateRetentionPolicy()).OperationKind);
        Assert.Equal(MemoryOperationKind.OperationStatus, MemoryOperationRequestBuilder.Cancellation(
            caller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationCancellationRequest(MemoryOperationId.New(), "user cancelled"),
            CreateRetentionPolicy()).OperationKind);
        Assert.Equal(MemoryOperationKind.EventAcknowledge, MemoryOperationRequestBuilder.EventAcknowledge(
            caller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.EventsProviderPush),
            new MemoryEventAcknowledgeRequest(MemoryProviderEventId.New(), Accepted: true, "accepted"),
            CreateRetentionPolicy()).OperationKind);
        Assert.Equal(MemoryOperationKind.SourceRequest, MemoryOperationRequestBuilder.SourceRequest(
            caller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.IngestionProviderRequestedSource),
            new MemorySourceRequest(
                MemorySourceRequestId.Parse("source-request-1"),
                [MemorySourceScope.Project],
                "Project facts",
                "Provider requested project facts."),
            CreateRetentionPolicy()).OperationKind);
    }

    public static IEnumerable<object[]> CrossCallerRoutes()
    {
        yield return [MemoryOperationCaller.Tool("agent.tool.memory-query", CreateRequester())];
        yield return [MemoryOperationCaller.WorkflowExecutor("workflow.executor.memory-query", CreateRequester())];
        yield return [MemoryOperationCaller.ContextContributor("context.contributor.memory-query", CreateRequester())];
        yield return [MemoryOperationCaller.UiAction("memory.admin.query", CreateRequester())];
        yield return [MemoryOperationCaller.ApiEndpoint("api.memory.query", CreateRequester())];
    }

    private static ServiceProvider CreateServiceProvider(bool enableMockDriver)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-operation-handler-{Guid.NewGuid():N}"));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddGenericMemoryModule(options =>
        {
            options.EnableDeterministicMockProvider = enableMockDriver;
        });
        return services.BuildServiceProvider(validateScopes: true);
    }

    private static MemoryContextQueryRequest CreateQueryRequest()
    {
        return new MemoryContextQueryRequest(
            "payment integration",
            [MemoryCapabilityIds.ContextQuerySync],
            new MemorySourceProvenance(
                MemorySourceSnapshotId.Parse("snapshot.project.1"),
                SourceModule: nameof(MemorySourceKind.Project),
                SourceRecordIds: ["project-1"],
                Citations: ["Project 1"]));
    }

    private static MemoryIngestionRequest CreateIngestionRequest()
    {
        return new MemoryIngestionRequest(
            MemorySourceSnapshotId.Parse("snapshot.project.1"),
            MemorySourceKind.Project,
            MemoryPayload.FromText("Project fact"),
            [MemoryCapabilityIds.IngestionSnapshot]);
    }

    private static MemoryFeedbackRequest CreateFeedbackRequest()
    {
        return new MemoryFeedbackRequest(
            MemoryContextPackId.New(),
            MemoryFeedbackOutcome.Useful,
            Comment: "useful",
            EconomicImpact: null);
    }

    private static MemoryProviderProfile CreateProviderProfile(MemoryCapabilityId capability)
    {
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse("provider.mock"),
            DisplayName: "Deterministic mock memory",
            MemoryProviderDriverKind.Mock,
            IsEnabled: true,
            MemoryProviderHealthState.Healthy,
            MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: ["test"],
            MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(
                MemoryProviderKind.Parse("provider.mock"),
                MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(capability, Version: "1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));
    }

    private static MemoryLedgerRequester CreateRequester()
    {
        return new MemoryLedgerRequester(
            RequesterId: "user-42",
            AgentId: "agent-dev",
            AgentRole: "developer",
            SessionId: "session-1",
            WorkflowId: "workflow-1",
            WorkflowNodeId: "node-1",
            ProcessId: "process-1",
            ProcessStepId: "step-1");
    }

    private static MemoryLedgerRetentionPolicy CreateRetentionPolicy()
    {
        return MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(7), Now.AddDays(30));
    }

    private sealed class FakeToolMemoryRoute(IMemoryOperationHandler handler)
    {
        public Task<MemoryOperationHandlerResult<MemoryContextPack>> QueryAsync(
            MemoryContextQueryRequest query,
            CancellationToken cancellationToken = default) =>
            handler.ExecuteQueryAsync(
                MemoryOperationRequestBuilder.Query(
                    MemoryOperationCaller.Tool("agent.tool.memory-query", CreateRequester()),
                    MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync),
                    query,
                    CreateRetentionPolicy()),
                cancellationToken);
    }

    private sealed class FakeWorkflowExecutorMemoryRoute(IMemoryOperationHandler handler)
    {
        public Task<MemoryOperationHandlerResult<MemoryContextPack>> QueryAsync(
            MemoryContextQueryRequest query,
            CancellationToken cancellationToken = default) =>
            handler.ExecuteQueryAsync(
                MemoryOperationRequestBuilder.Query(
                    MemoryOperationCaller.WorkflowExecutor("workflow.executor.memory-query", CreateRequester()),
                    MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync),
                    query,
                    CreateRetentionPolicy()),
                cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
