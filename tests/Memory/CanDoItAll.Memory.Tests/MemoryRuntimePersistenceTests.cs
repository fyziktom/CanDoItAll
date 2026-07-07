using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Memory.Tests;

public sealed class MemoryRuntimePersistenceTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-07-05T12:00:00Z");

    [Fact]
    public async Task RT001_Zero_provider_registration_returns_typed_no_provider_and_no_driver_dispatch()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: false);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var runtime = provider.GetRequiredService<IMemoryRuntimeService>();

        var result = await runtime.ExecuteContextQueryAsync(CreateRuntimeRequest(), CreateQueryRequest());

        Assert.Equal(MemoryProviderSelectionStatus.NoProviderConfigured, result.Selection.Status);
        Assert.False(result.Selection.DispatchAllowed);
        Assert.False(result.DriverDispatchAttempted);
        Assert.Null(result.ContextPack);
        Assert.Empty(provider.GetServices<IMemoryProviderDriver>());
    }

    [Fact]
    public async Task RT002_Provider_profiles_persist_and_rehydrate_registry_contracts()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: false);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var store = provider.GetRequiredService<IMemoryProviderProfileStore>();
        var profile = CreateMockProviderProfile();

        await store.UpsertAsync(profile, Now);
        var profiles = await store.ListAsync();

        var persisted = Assert.Single(profiles);
        Assert.Equal(profile.InstanceId, persisted.InstanceId);
        Assert.Equal(MemoryProviderDriverKind.Mock, persisted.DriverKind);
        Assert.Contains(persisted.Manifest.Capabilities, capability => capability.Id == MemoryCapabilityId.Parse("context.query.sync"));
    }

    [Fact]
    public async Task RT003_Operation_ledger_persists_and_updates_status()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: false);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var store = provider.GetRequiredService<IMemoryOperationLedgerStore>();
        var operation = CreateOperationRecord();

        await store.CreateAsync(operation);
        var running = await store.TransitionAsync(operation.OperationId, MemoryLedgerStatus.Running, Now.AddSeconds(5), "worker started");
        var persisted = await store.GetAsync(operation.OperationId);

        Assert.Equal(MemoryLedgerStatus.Running, running.Status);
        Assert.Equal(MemoryLedgerStatus.Running, persisted?.Status);
        Assert.Equal(1, persisted?.TransitionCount);
        Assert.Equal("worker started", persisted?.StatusReason);
    }

    [Fact]
    public async Task RT004_Feedback_event_and_source_request_ledgers_persist_generic_metadata()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: false);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var feedbackStore = provider.GetRequiredService<IMemoryFeedbackLedgerStore>();
        var eventStore = provider.GetRequiredService<IMemoryEventLedgerStore>();
        var sourceStore = provider.GetRequiredService<IMemorySourceRequestLedgerStore>();

        await feedbackStore.SubmitAsync(CreateFeedbackRecord());
        await eventStore.EnqueueInboxAsync(CreateInboxRecord());
        await eventStore.EnqueueOutboxAsync(CreateOutboxRecord());
        await sourceStore.EnqueueAsync(CreateSourceJobRecord());

        Assert.Single(await feedbackStore.ListByProviderAsync(MemoryProviderInstanceId.Parse("provider.mock")));
        Assert.Single(await eventStore.ListPendingInboxAsync(MemoryProviderInstanceId.Parse("provider.mock")));
        Assert.Single(await eventStore.ListPendingOutboxAsync(MemoryProviderInstanceId.Parse("provider.mock")));
        Assert.Single(await sourceStore.ListByProviderAsync(MemoryProviderInstanceId.Parse("provider.mock")));
    }

    [Fact]
    public async Task RT005_Explicit_mock_driver_profile_dispatches_deterministically_when_enabled()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: true);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var store = provider.GetRequiredService<IMemoryProviderProfileStore>();
        await store.UpsertAsync(CreateMockProviderProfile(), Now);

        var runtime = provider.GetRequiredService<IMemoryRuntimeService>();
        var driver = provider.GetRequiredService<DeterministicMockMemoryProviderDriver>();
        var result = await runtime.ExecuteContextQueryAsync(CreateRuntimeRequest(), CreateQueryRequest());

        Assert.Equal(MemoryProviderSelectionStatus.Selected, result.Selection.Status);
        Assert.True(result.DriverDispatchAttempted);
        Assert.Equal(1, driver.DispatchCount);
        Assert.Equal("Mock memory context for payment integration", result.ContextPack?.Summary);
    }

    [Fact]
    public async Task RT006_Retention_projection_returns_due_generic_ledger_candidates()
    {
        using var rootProvider = CreateServiceProvider(enableMockDriver: false);
        using var scope = rootProvider.CreateScope();
        var provider = scope.ServiceProvider;
        var operationStore = provider.GetRequiredService<IMemoryOperationLedgerStore>();
        var feedbackStore = provider.GetRequiredService<IMemoryFeedbackLedgerStore>();
        var eventStore = provider.GetRequiredService<IMemoryEventLedgerStore>();
        var retentionStore = provider.GetRequiredService<IMemoryRetentionProjectionStore>();

        await operationStore.CreateAsync(CreateOperationRecord(
            MemoryLedgerRetentionPolicy.Expiring(Now.AddHours(-2), Now.AddDays(2))));
        await feedbackStore.SubmitAsync(CreateFeedbackRecord(
            MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(2), Now.AddDays(3))));
        await eventStore.EnqueueInboxAsync(CreateInboxRecord(
            MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(-4), Now.AddDays(-1))));

        var candidates = await retentionStore.ListDueAsync(Now, take: 10);

        Assert.Contains(candidates, candidate =>
            candidate.LedgerName == MemoryLedgerPersistenceContract.OperationRecords
            && candidate.Decision == MemoryLedgerRetentionDecision.Expire);
        Assert.DoesNotContain(candidates, candidate =>
            candidate.LedgerName == MemoryLedgerPersistenceContract.FeedbackRecords);
        Assert.Contains(candidates, candidate =>
            candidate.LedgerName == MemoryLedgerPersistenceContract.EventInboxRecords
            && candidate.Decision == MemoryLedgerRetentionDecision.Forget);
    }

    private static ServiceProvider CreateServiceProvider(bool enableMockDriver)
    {
        var services = new ServiceCollection();
        services.AddDbContextFactory<AppDbContext>(options =>
            options.UseInMemoryDatabase($"memory-runtime-{Guid.NewGuid():N}"));
        services.AddGenericMemoryModule(options =>
        {
            options.EnableDeterministicMockProvider = enableMockDriver;
        });

        return services.BuildServiceProvider(validateScopes: true);
    }

    private static MemoryRuntimeOperationRequest CreateRuntimeRequest()
    {
        return new MemoryRuntimeOperationRequest(
            MemoryProviderSelectionPolicy.RequireCapability(MemoryCapabilityId.Parse("context.query.sync")),
            MemoryProviderSelectionContext.None,
            MemoryOperationKind.ContextQuery,
            CreateRequester(),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [MemorySourceSnapshotId.Parse("snapshot.project.1")],
            CreateRetentionPolicy());
    }

    private static MemoryContextQueryRequest CreateQueryRequest()
    {
        return new MemoryContextQueryRequest(
            "payment integration",
            [MemoryCapabilityId.Parse("context.query.sync")],
            new MemorySourceProvenance(
                MemorySourceSnapshotId.Parse("snapshot.project.1"),
                SourceModule: nameof(MemorySourceKind.Project),
                SourceRecordIds: ["project-1"],
                Citations: ["Project 1"]));
    }

    private static MemoryProviderProfile CreateMockProviderProfile()
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
                [new MemoryCapabilityDescriptor(MemoryCapabilityId.Parse("context.query.sync"), Version: "1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly,
                UiSurfaces: [],
                MemoryProviderLimits.Default,
                MemoryExtensionData.Empty));
    }

    private static MemoryOperationRecord CreateOperationRecord(MemoryLedgerRetentionPolicy? retention = null)
    {
        return MemoryOperationRecord.Create(
            MemoryOperationRecordId.New(),
            MemoryOperationId.New(),
            MemoryProviderInstanceId.Parse("provider.mock"),
            MemoryCapabilityId.Parse("context.query.sync"),
            MemoryOperationKind.ContextQuery,
            CreateRequester(),
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            [MemorySourceSnapshotId.Parse("snapshot.project.1")],
            retention ?? CreateRetentionPolicy(),
            Now);
    }

    private static MemoryFeedbackRecord CreateFeedbackRecord(MemoryLedgerRetentionPolicy? retention = null)
    {
        return MemoryFeedbackRecord.CreateUnmatched(
            MemoryFeedbackRecordId.New(),
            MemoryProviderInstanceId.Parse("provider.mock"),
            MemoryFeedbackStage.ContextUsed,
            MemoryFeedbackOutcome.Useful,
            CreateRequester(),
            unmatchedReason: "manual feedback before delivery",
            retention ?? CreateRetentionPolicy(),
            Now);
    }

    private static MemoryEventInboxRecord CreateInboxRecord(MemoryLedgerRetentionPolicy? retention = null)
    {
        var eventId = MemoryProviderEventId.New();
        return MemoryEventInboxRecord.Create(
            MemoryEventInboxRecordId.New(),
            MemoryProviderInstanceId.Parse("provider.mock"),
            eventId,
            MemoryProviderEventKind.VerificationRequest,
            MemoryCorrelationId.New(),
            MemoryCausationId.New(),
            MemoryEventPriority.Normal,
            MemoryEventLoopContext.ProviderOrigin(MemoryProviderInstanceId.Parse("provider.mock")),
            retention ?? CreateRetentionPolicy(),
            Now);
    }

    private static MemoryEventOutboxRecord CreateOutboxRecord()
    {
        return MemoryEventOutboxRecord.CreateAcknowledgement(
            MemoryEventOutboxRecordId.New(),
            MemoryProviderInstanceId.Parse("provider.mock"),
            MemoryProviderEventId.New(),
            inboxRecordId: null,
            Now,
            MemoryPayload.FromJson(JsonDocument.Parse("""{"accepted":true}""").RootElement));
    }

    private static MemorySourceIngestionJobRecord CreateSourceJobRecord()
    {
        return new MemorySourceIngestionJobRecord(
            Guid.NewGuid(),
            MemoryProviderInstanceId.Parse("provider.mock"),
            new MemorySourceGatewayRequest(
                CanDoItAll.AgentFramework.Core.MemorySourceKind.WorkbenchProjectStructure,
                Guid.Parse("4a7f8bf0-b2a5-4cc7-8229-322729fb9168"),
                MemorySourceScope.Project,
                Cursor: null,
                Take: null,
                MemorySourceGatewayPolicy.Allow([CanDoItAll.AgentFramework.Core.MemorySourceKind.WorkbenchProjectStructure]),
                RequesterId: "user-42"),
            MemorySourceIngestionJobStatus.Queued,
            CreatedAtUtc: Now,
            UpdatedAtUtc: Now,
            StatusReason: "queued");
    }

    private static MemoryLedgerRequester CreateRequester()
    {
        return new MemoryLedgerRequester(
            RequesterId: "user-42",
            AgentId: null,
            AgentRole: null,
            SessionId: "session-1",
            WorkflowId: null,
            WorkflowNodeId: null,
            ProcessId: null,
            ProcessStepId: null);
    }

    private static MemoryLedgerRetentionPolicy CreateRetentionPolicy()
    {
        return MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(7), Now.AddDays(30));
    }
}
