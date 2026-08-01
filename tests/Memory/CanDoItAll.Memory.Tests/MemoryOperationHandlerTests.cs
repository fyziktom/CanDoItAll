using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Mock;
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
        Assert.Null(toolResult.Output?.FeedbackHandle);
        Assert.Null(executorResult.Output?.FeedbackHandle);
        Assert.Null(toolResult.FeedbackHandle);
        Assert.Null(executorResult.FeedbackHandle);
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
    public async Task Feedback_claim_without_delivery_driver_is_rejected_before_enqueue()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: false);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var profile = CreateProviderProfile(MemoryCapabilityIds.FeedbackImmediate);
        await provider.GetRequiredService<IMemoryProviderProfileStore>().UpsertAsync(profile, Now);

        var result = await provider.GetRequiredService<IMemoryOperationHandler>()
            .SubmitFeedbackAsync(MemoryOperationRequestBuilder.Feedback(
                MemoryOperationCaller.UiAction("memory.feedback.test", CreateRequester()),
                MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.FeedbackImmediate) with
                {
                    ExplicitProviderId = profile.InstanceId
                },
                CreateFeedbackRequest(),
                CreateRetentionPolicy()));

        Assert.Equal(MemoryOperationHandlerStatus.DriverUnavailable, result.Status);
        Assert.False(result.DriverDispatchAttempted);
        Assert.Empty(await provider.GetRequiredService<IMemoryFeedbackLedgerStore>()
            .ListByProviderAsync(profile.InstanceId));
    }

    [Fact]
    public async Task Event_acknowledgement_claim_without_outbox_driver_is_rejected_before_enqueue()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: false);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var profile = CreateProviderProfile(MemoryCapabilityIds.EventsProviderPush);
        await provider.GetRequiredService<IMemoryProviderProfileStore>().UpsertAsync(profile, Now);

        var result = await provider.GetRequiredService<IMemoryOperationHandler>()
            .AcknowledgeEventAsync(MemoryOperationRequestBuilder.EventAcknowledge(
                MemoryOperationCaller.UiAction("memory.event.test", CreateRequester()),
                MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.EventsProviderPush) with
                {
                    ExplicitProviderId = profile.InstanceId
                },
                new MemoryEventAcknowledgeRequest(MemoryProviderEventId.New(), Accepted: true, "accepted"),
                CreateRetentionPolicy()));

        Assert.Equal(MemoryOperationHandlerStatus.DriverUnavailable, result.Status);
        Assert.False(result.DriverDispatchAttempted);
        Assert.Empty(await provider.GetRequiredService<IMemoryEventLedgerStore>()
            .ListPendingOutboxAsync(profile.InstanceId));
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

    [Fact]
    public async Task Operation_status_and_cancellation_reject_a_different_requester()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: true);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var profile = CreateProviderProfile(MemoryCapabilityIds.ContextQuerySync);
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, Now);
        var handler = provider.GetRequiredService<IMemoryOperationHandler>();
        var queryPolicy = MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync) with
        {
            ExplicitProviderId = profile.InstanceId
        };
        var queryResult = await handler.ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
            MemoryOperationCaller.Tool("agent.tool.memory-query", CreateRequester()),
            queryPolicy,
            CreateQueryRequest(),
            CreateRetentionPolicy()));
        var operationId = Assert.IsType<MemoryOperationRecord>(queryResult.OperationRecord).OperationId;
        var foreignCaller = MemoryOperationCaller.Tool(
            "agent.tool.memory-status",
            CreateRequester() with
            {
                RequesterId = "user-foreign",
                AgentId = "agent-foreign",
                SessionId = "session-foreign"
            });
        var statusResult = await handler.GetStatusAsync(MemoryOperationRequestBuilder.Status(
            foreignCaller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationStatusRequest(operationId),
            CreateRetentionPolicy()));
        var cancellationResult = await handler.CancelAsync(MemoryOperationRequestBuilder.Cancellation(
            foreignCaller,
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationCancellationRequest(operationId, "foreign cancellation"),
            CreateRetentionPolicy()));

        Assert.NotEqual(MemoryOperationHandlerStatus.Completed, statusResult.Status);
        Assert.NotEqual(MemoryOperationHandlerStatus.Cancelled, cancellationResult.Status);
        Assert.False(statusResult.DriverDispatchAttempted);
        Assert.False(cancellationResult.DriverDispatchAttempted);
    }

    [Fact]
    public async Task Status_and_cancellation_do_not_succeed_when_provider_selection_is_rejected()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: true);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var profile = CreateProviderProfile(MemoryCapabilityIds.ContextQuerySync);
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, Now);
        var handler = provider.GetRequiredService<IMemoryOperationHandler>();
        var requester = CreateRequester();
        var queryResult = await handler.ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
            MemoryOperationCaller.Tool("agent.tool.memory-query", requester),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync) with
            {
                ExplicitProviderId = profile.InstanceId
            },
            CreateQueryRequest(),
            CreateRetentionPolicy()));
        var operationId = Assert.IsType<MemoryOperationRecord>(queryResult.OperationRecord).OperationId;

        var statusResult = await handler.GetStatusAsync(MemoryOperationRequestBuilder.Status(
            MemoryOperationCaller.Tool("agent.tool.memory-status", requester),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationStatusRequest(operationId),
            CreateRetentionPolicy()));
        var cancellationResult = await handler.CancelAsync(MemoryOperationRequestBuilder.Cancellation(
            MemoryOperationCaller.Tool("agent.tool.memory-cancel", requester),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.OperationStatus),
            new MemoryOperationCancellationRequest(operationId, "user cancelled"),
            CreateRetentionPolicy()));

        Assert.Equal(MemoryOperationHandlerStatus.CapabilityUnavailable, statusResult.Status);
        Assert.Equal(MemoryOperationHandlerStatus.CapabilityUnavailable, cancellationResult.Status);
        Assert.Null(statusResult.OperationRecord);
        Assert.Null(cancellationResult.OperationRecord);
        Assert.False(statusResult.DriverDispatchAttempted);
        Assert.False(cancellationResult.DriverDispatchAttempted);
    }

    [Fact]
    public async Task Driver_exception_becomes_a_typed_failure_without_exposing_exception_details()
    {
        var driver = new ThrowingMemoryProviderDriver();
        using var rootProvider = CreateServiceProvider(enableMockDriver: false, driver);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var profile = CreateProviderProfile(
            MemoryCapabilityIds.ContextQuerySync,
            MemoryProviderDriverKind.Mock);
        await provider.GetRequiredService<IMemoryProviderProfileStore>()
            .UpsertAsync(profile, Now);
        var handler = provider.GetRequiredService<IMemoryOperationHandler>();

        var result = await handler.ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
            MemoryOperationCaller.Tool("agent.tool.memory-query", CreateRequester()),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync) with
            {
                ExplicitProviderId = profile.InstanceId
            },
            CreateQueryRequest(),
            CreateRetentionPolicy()));

        Assert.Equal(MemoryOperationHandlerStatus.DriverFailed, result.Status);
        Assert.Equal(MemoryLedgerStatus.Failed, result.OperationRecord?.Status);
        Assert.True(result.DriverDispatchAttempted);
        Assert.DoesNotContain("secret-provider-token", result.Diagnostic, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Provider_configuration_exception_becomes_a_typed_failure_before_dispatch()
    {
        using var rootProvider = CreateServiceProvider(
            enableMockDriver: false,
            useThrowingProfileStore: true);
        using var scope = rootProvider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<IMemoryOperationHandler>();

        var result = await handler.ExecuteQueryAsync(MemoryOperationRequestBuilder.Query(
            MemoryOperationCaller.Tool("agent.tool.memory-query", CreateRequester()),
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync),
            CreateQueryRequest(),
            CreateRetentionPolicy()));

        Assert.Equal(MemoryOperationHandlerStatus.ProviderConfigurationFailed, result.Status);
        Assert.Equal(MemoryProviderSelectionStatus.ProviderConfigurationFailed, result.Selection.Status);
        Assert.False(result.DriverDispatchAttempted);
        Assert.DoesNotContain("secret-connection-string", result.Diagnostic, StringComparison.Ordinal);
    }

    public static IEnumerable<object[]> CrossCallerRoutes()
    {
        yield return [MemoryOperationCaller.Tool("agent.tool.memory-query", CreateRequester())];
        yield return [MemoryOperationCaller.WorkflowExecutor("workflow.executor.memory-query", CreateRequester())];
        yield return [MemoryOperationCaller.ContextContributor("context.contributor.memory-query", CreateRequester())];
        yield return [MemoryOperationCaller.UiAction("memory.admin.query", CreateRequester())];
        yield return [MemoryOperationCaller.ApiEndpoint("api.memory.query", CreateRequester())];
    }

    private static ServiceProvider CreateServiceProvider(
        bool enableMockDriver,
        IMemoryProviderDriver? driver = null,
        bool useThrowingProfileStore = false)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-operation-handler-{Guid.NewGuid():N}"));
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(Now));
        services.AddGenericMemoryModule();
        if (enableMockDriver)
        {
            services.AddDeterministicMockMemoryProviderDriver();
        }
        if (driver is not null)
        {
            services.AddSingleton<IMemoryProviderDriver>(driver);
        }

        if (useThrowingProfileStore)
        {
            services.AddScoped<IMemoryProviderProfileStore, ThrowingMemoryProviderProfileStore>();
        }

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

    private static MemoryProviderProfile CreateProviderProfile(
        MemoryCapabilityId capability,
        MemoryProviderDriverKind driverKind = MemoryProviderDriverKind.Mock)
    {
        return new MemoryProviderProfile(
            MemoryProviderInstanceId.Parse("provider.mock"),
            DisplayName: "Deterministic mock memory",
            driverKind,
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
                    MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync) with
                    {
                        ExplicitProviderId = MemoryProviderInstanceId.Parse("provider.mock")
                    },
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
                    MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityIds.ContextQuerySync) with
                    {
                        ExplicitProviderId = MemoryProviderInstanceId.Parse("provider.mock")
                    },
                    query,
                    CreateRetentionPolicy()),
                cancellationToken);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class ThrowingMemoryProviderDriver : IMemoryProviderDriver
    {
        public MemoryProviderDriverKind DriverKind => MemoryProviderDriverKind.Mock;

        public Task<MemoryProviderDriverResult> ExecuteContextQueryAsync(
            MemoryProviderProfile provider,
            MemoryOperationRecord operation,
            MemoryContextQueryRequest request,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("secret-provider-token");
        }
    }

    private sealed class ThrowingMemoryProviderProfileStore : IMemoryProviderProfileStore
    {
        public Task UpsertAsync(
            MemoryProviderProfile profile,
            DateTimeOffset updatedAtUtc,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("secret-connection-string");
        }

        public Task<MemoryProviderProfile?> GetAsync(
            MemoryProviderInstanceId providerId,
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("secret-connection-string");
        }

        public Task<IReadOnlyList<MemoryProviderProfile>> ListAsync(
            CancellationToken cancellationToken = default)
        {
            throw new InvalidOperationException("secret-connection-string");
        }
    }
}
