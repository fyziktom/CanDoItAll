using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence;
using CanDoItAll.Memory.Persistence.Hosting;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Memory;

public sealed class MemoryOwnerPersistenceTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromSeconds(30);
    private static readonly MemoryProviderInstanceId ProviderId = MemoryProviderInstanceId.Parse("provider.owner-persistence");

    [Fact]
    public async Task Runtime_model_retains_complete_schema_mappings_without_foreign_entities() {
        await using var application = await TestApplication.CreateAsync();
        var ownerFactory = application.Services.GetRequiredService<IDbContextFactory<MemoryDbContext>>();
        var schemaFactory = application.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var owner = await ownerFactory.CreateDbContextAsync();
        await using var schema = await schemaFactory.CreateDbContextAsync();
        var ownerModel = owner.GetService<IDesignTimeModel>().Model;
        var schemaModel = schema.GetService<IDesignTimeModel>().Model;
        string[] tables = ["Memory_ProviderProfiles", "Memory_OperationLedger", "Memory_FeedbackLedger",
            "Memory_EventInbox", "Memory_EventOutbox", "Memory_SourceRequests", "Memory_WorkerLeases"];

        Assert.Equal(tables.Order(), ownerModel.GetEntityTypes().Select(entity => entity.GetTableName()).Order());
        foreach (var entity in ownerModel.GetEntityTypes()) {
            Assert.Equal(typeof(MemoryDbContext).Namespace, entity.ClrType.Namespace);
            var completeEntity = Assert.IsAssignableFrom<IEntityType>(schemaModel.FindEntityType(entity.ClrType));
            Assert.Equal(completeEntity.ToDebugString(MetadataDebugStringOptions.LongDefault),
                entity.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
    }

    [Fact]
    public async Task Canonical_records_survive_restart_and_owner_writes_remain_profile_isolated() {
        await using var environment = CanDoItAllTestEnvironment.Create("memory-owner-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        var otherProfile = environment.CreatePostgreSqlProfile("other");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var records = CreateRecords();
        var originalToken = Guid.NewGuid();
        await using (var beforeRestart = await TestApplication.CreateAsync(options)) {
            var factory = beforeRestart.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var schema = await factory.CreateDbContextAsync();
            var savedProfile = MemoryProviderProfileEntity.FromProfile(records.Profile, Now);
            savedProfile.ConcurrencyToken = originalToken;
            schema.AddRange(savedProfile,
                MemoryOperationLedgerEntity.FromRecord(records.Operation),
                MemoryFeedbackLedgerEntity.FromRecord(records.Feedback),
                MemoryEventInboxLedgerEntity.FromRecord(records.Inbox),
                MemoryEventOutboxLedgerEntity.FromRecord(records.Outbox),
                MemorySourceRequestLedgerEntity.FromRecord(records.SourceRequest));
            await schema.SaveChangesAsync();
        }

        await using (var afterRestart = await TestApplication.CreateAsync(options)) {
            await using var scope = afterRestart.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            await AssertRecordsAsync(services, records);
            var writtenAtUtc = Now.AddMinutes(1);
            var profileStore = services.GetRequiredService<IMemoryProviderProfileStore>();
            var operations = services.GetRequiredService<IMemoryOperationLedgerStore>();
            var feedback = services.GetRequiredService<IMemoryFeedbackLedgerStore>();
            var events = services.GetRequiredService<IMemoryEventLedgerStore>();
            var sources = services.GetRequiredService<IMemorySourceRequestLedgerStore>();
            await profileStore.UpsertAsync(records.Profile with { DisplayName = "Edited through Memory owner" }, writtenAtUtc);
            await operations.TransitionAsync(records.Operation.OperationId, MemoryLedgerStatus.Running, writtenAtUtc, "owner resumed");
            await feedback.DeferAsync(records.Feedback.FeedbackRecordId, writtenAtUtc, incrementRetry: true);
            await events.DeferInboxAsync(records.Inbox.InboxRecordId, writtenAtUtc, "owner deferred", incrementRetry: true);
            await events.DeferOutboxAsync(records.Outbox.OutboxRecordId, writtenAtUtc, incrementRetry: true);
            var additionalSourceRequest = records.SourceRequest with { JobId = Guid.NewGuid(), CreatedAtUtc = writtenAtUtc, UpdatedAtUtc = writtenAtUtc };
            await sources.EnqueueAsync(additionalSourceRequest);
            Assert.Equal(2, (await sources.ListByProviderAsync(ProviderId)).Count);

            var retention = services.GetRequiredService<IMemoryRetentionProjectionStore>();
            var expiredAtUtc = Now.AddDays(8);
            var candidates = await retention.ListDueAsync(expiredAtUtc, take: 10);
            Assert.Equal(3, candidates.Count);
            var operationCandidate = Assert.Single(candidates,
                candidate => candidate.LedgerName == MemoryLedgerPersistenceContract.OperationRecords);
            Assert.Equal(MemoryLedgerRetentionDecision.Expire, operationCandidate.Decision);
            await retention.ApplyAsync(operationCandidate, expiredAtUtc, "owner retention");

            records = records with {
                Profile = records.Profile with { DisplayName = "Edited through Memory owner" },
                Operation = records.Operation with {
                    Status = MemoryLedgerStatus.Expired, TransitionCount = 2, UpdatedAtUtc = expiredAtUtc,
                    CompletedAtUtc = expiredAtUtc, StatusReason = "owner retention"
                },
                Feedback = records.Feedback with { RetryCount = 1, UpdatedAtUtc = writtenAtUtc },
                Inbox = records.Inbox with { RetryCount = 1, UpdatedAtUtc = writtenAtUtc, StatusReason = "owner deferred" },
                Outbox = records.Outbox with { RetryCount = 1, UpdatedAtUtc = writtenAtUtc }
            };
            await AssertRecordsAsync(services, records);
            var ownerFactory = services.GetRequiredService<IDbContextFactory<MemoryDbContext>>();
            await using var readback = await ownerFactory.CreateDbContextAsync();
            var savedProfile = await readback.Set<MemoryProviderProfileEntity>().SingleAsync();
            Assert.NotEqual(originalToken, savedProfile.ConcurrencyToken);
            Assert.Equal(Now, savedProfile.CreatedAtUtc);
            Assert.Equal(writtenAtUtc, savedProfile.UpdatedAtUtc);
            var savedOperation = await readback.Set<MemoryOperationLedgerEntity>().SingleAsync();
            Assert.Equal((int)MemoryLedgerStatus.Expired, savedOperation.Status);
            Assert.Equal(expiredAtUtc, savedOperation.CompletedAtUtc);
            Assert.Equal(records.Operation.Retention.ExpiresAtUtc, savedOperation.ExpiresAtUtc);

            await using var other = await TestApplication.CreateAsync(new TestHarnessOptions {
                TestEnvironment = environment, ActiveProfile = otherProfile
            });
            await using var otherScope = other.Services.CreateAsyncScope();
            var otherServices = otherScope.ServiceProvider;
            Assert.Empty(await otherServices.GetRequiredService<IMemoryProviderProfileStore>().ListAsync());
            Assert.Null(await otherServices.GetRequiredService<IMemoryOperationLedgerStore>().GetAsync(records.Operation.OperationId));
            var otherFactory = otherServices.GetRequiredService<IDbContextFactory<MemoryDbContext>>();
            await using var otherContext = await otherFactory.CreateDbContextAsync();
            Assert.NotEqual(readback.Database.GetConnectionString(), otherContext.Database.GetConnectionString());
            await otherServices.GetRequiredService<IMemoryProviderProfileStore>()
                .UpsertAsync(records.Profile with { DisplayName = "Other database" }, writtenAtUtc);
            await AssertRecordsAsync(services, records);
        }

        await using var secondRestart = await TestApplication.CreateAsync(options);
        await using var restartedScope = secondRestart.Services.CreateAsyncScope();
        await AssertRecordsAsync(restartedScope.ServiceProvider, records);
    }

    [Fact]
    public async Task PostgreSql_lease_ownership_survives_restart_and_rejects_expired_or_forged_owners() {
        await using var environment = CanDoItAllTestEnvironment.Create("memory-owner-leases");
        var profile = environment.CreatePostgreSqlProfile("leases");
        var options = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile };
        var firstOwner = MemoryWorkerLeaseOwnerId.Parse("memory-replica-a");
        var secondOwner = MemoryWorkerLeaseOwnerId.Parse("memory-replica-b");
        MemoryWorkerLease first;
        await using (var beforeRestart = await TestApplication.CreateAsync(options)) {
            await using var scope = beforeRestart.Services.CreateAsyncScope();
            var store = scope.ServiceProvider.GetRequiredService<IMemoryWorkerLeaseStore>();
            first = Assert.IsType<MemoryWorkerLease>(await store.TryAcquireAsync(
                MemoryBackgroundWorkerPhase.OperationPolling, firstOwner, Now, LeaseDuration));
            Assert.Equal(Now.Add(LeaseDuration), first.ExpiresAtUtc);
        }

        await using var restarted = await TestApplication.CreateAsync(options);
        await using var replica = await TestApplication.CreateAsync(options);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        await using var replicaScope = replica.Services.CreateAsyncScope();
        var restartedStore = restartedScope.ServiceProvider.GetRequiredService<IMemoryWorkerLeaseStore>();
        var replicaStore = replicaScope.ServiceProvider.GetRequiredService<IMemoryWorkerLeaseStore>();
        Assert.Null(await replicaStore.TryAcquireAsync(first.Phase, secondOwner, Now.AddSeconds(1), LeaseDuration));
        var forgedToken = first with { Token = MemoryWorkerLeaseToken.New() };
        var forgedOwner = first with { OwnerId = secondOwner };
        foreach (var forged in new[] { forgedToken, forgedOwner }) {
            Assert.False(await replicaStore.RenewAsync(forged, Now.AddSeconds(2), LeaseDuration));
            Assert.False(await replicaStore.CompleteAsync(forged, Now.AddSeconds(2)));
            Assert.False(await replicaStore.ReleaseAsync(forged, Now.AddSeconds(2)));
        }

        Assert.True(await restartedStore.RenewAsync(first, Now.AddSeconds(20), LeaseDuration));
        Assert.Null(await replicaStore.TryAcquireAsync(first.Phase, secondOwner, Now.AddSeconds(49), LeaseDuration));
        Assert.False(await restartedStore.RenewAsync(first, Now.AddSeconds(50), LeaseDuration));
        Assert.False(await restartedStore.CompleteAsync(first, Now.AddSeconds(50)));
        var recovered = Assert.IsType<MemoryWorkerLease>(await replicaStore.TryAcquireAsync(
            first.Phase, secondOwner, Now.AddSeconds(50), LeaseDuration));
        Assert.NotEqual(first.Token, recovered.Token);
        Assert.False(await restartedStore.RenewAsync(first, Now.AddSeconds(51), LeaseDuration));
        Assert.False(await restartedStore.CompleteAsync(first, Now.AddSeconds(51)));
        Assert.False(await restartedStore.ReleaseAsync(first, Now.AddSeconds(51)));
        Assert.True(await replicaStore.CompleteAsync(recovered, Now.AddSeconds(51)));

        var released = Assert.IsType<MemoryWorkerLease>(await restartedStore.TryAcquireAsync(
            first.Phase, firstOwner, Now.AddSeconds(52), LeaseDuration));
        Assert.True(await restartedStore.ReleaseAsync(released, Now.AddSeconds(82)));
        Assert.NotNull(await replicaStore.TryAcquireAsync(first.Phase, secondOwner, Now.AddSeconds(82), LeaseDuration));
    }

    private static async Task AssertRecordsAsync(IServiceProvider services, SavedRecords expected) {
        AssertEquivalent(expected.Profile, await services.GetRequiredService<IMemoryProviderProfileStore>().GetAsync(ProviderId));
        var operation = Assert.IsType<MemoryOperationRecord>(await services.GetRequiredService<IMemoryOperationLedgerStore>()
            .GetAsync(expected.Operation.OperationId));
        AssertEquivalent(expected.Operation, operation);
        Assert.Equal(expected.Operation.SourceSnapshotIds, operation.SourceSnapshotIds);
        var context = operation.GetRequiredMemoryRequestContext();
        Assert.Equal("workspace-persisted", context.Workspace.WorkspaceId);
        Assert.Equal("project-persisted", context.Execution.ProjectId);
        Assert.Equal(MemorySensitivity.Confidential, context.Policy.Sensitivity);
        Assert.Equal(12_345, context.Budget.MaxSourceBytes);
        AssertEquivalent(expected.Feedback, Assert.Single(await services.GetRequiredService<IMemoryFeedbackLedgerStore>()
            .ListByProviderAsync(ProviderId)));
        var events = services.GetRequiredService<IMemoryEventLedgerStore>();
        Assert.True(await events.ContainsInboxDedupeKeyAsync(expected.Inbox.DedupeKey));
        AssertEquivalent(expected.Inbox, Assert.Single(await events.ListPendingInboxAsync(ProviderId)));
        AssertEquivalent(expected.Outbox, Assert.Single(await events.ListPendingOutboxAsync(ProviderId)));
        var sourceRequests = await services.GetRequiredService<IMemorySourceRequestLedgerStore>().ListByProviderAsync(ProviderId);
        AssertEquivalent(expected.SourceRequest, Assert.Single(sourceRequests, item => item.JobId == expected.SourceRequest.JobId));
    }

    private static void AssertEquivalent<T>(T expected, T? actual) where T : class {
        Assert.NotNull(actual);
        Assert.Equal(JsonSerializer.Serialize(expected, JsonSerializerOptions.Web),
            JsonSerializer.Serialize(actual, JsonSerializerOptions.Web));
    }

    private static SavedRecords CreateRecords() {
        var profile = new MemoryProviderProfile(ProviderId, "Saved before owner cutover", MemoryProviderDriverKind.Mock,
            IsEnabled: true, MemoryProviderHealthState.Healthy, MemoryProviderWorkspaceScope.AllWorkspaces,
            SelectionTags: ["owner-persistence"], MemoryProviderProfilePolicy.Default,
            new MemoryProviderManifest(MemoryProviderKind.Parse("provider.mock"), MemoryProtocolVersion.Current,
                [new MemoryCapabilityDescriptor(MemoryCapabilityIds.ContextQuerySync, Version: "1", Supported: true)],
                MemoryProviderInteractionSupport.SyncQueryOnly, UiSurfaces: [], MemoryProviderLimits.Default, MemoryExtensionData.Empty));
        var requester = new MemoryLedgerRequester("user-memory-owner", null, null, "session-persisted", null, null, null, null);
        var retention = MemoryLedgerRetentionPolicy.Expiring(Now.AddDays(7), Now.AddDays(30));
        var operation = MemoryOperationRecord.Create(MemoryOperationRecordId.New(), MemoryOperationId.New(), ProviderId,
            MemoryCapabilityIds.ContextQuerySync, MemoryOperationKind.ContextQuery, requester,
            MemoryCorrelationId.New(), MemoryCausationId.New(), [MemorySourceSnapshotId.Parse("snapshot.project.owner")], retention, Now);
        var requestContext = MemoryRequestContext.Default with {
            Workspace = new MemoryWorkspaceContext("workspace-persisted", "Persisted workspace", null, "engineering", ["test"]),
            Execution = new MemoryExecutionContext("project-persisted", "Persisted project", null, null, null, null, null, []),
            Policy = MemoryPolicyContext.InternalDefault with { Sensitivity = MemorySensitivity.Confidential },
            Budget = new MemoryBudget(10, 12_345, 2_000, TimeSpan.FromSeconds(10))
        };
        operation = operation with { Extensions = operation.Extensions.WithMemoryRequestContext(operation, requestContext) };
        var feedback = MemoryFeedbackRecord.CreateUnmatched(MemoryFeedbackRecordId.New(), ProviderId,
            MemoryFeedbackStage.ContextUsed, MemoryFeedbackOutcome.Useful, requester, "manual feedback", retention, Now);
        var inbox = MemoryEventInboxRecord.Create(MemoryEventInboxRecordId.New(), ProviderId, MemoryProviderEventId.New(),
            MemoryProviderEventKind.VerificationRequest, MemoryCorrelationId.New(), MemoryCausationId.New(),
            MemoryEventPriority.Normal, MemoryEventLoopContext.ProviderOrigin(ProviderId), retention, Now);
        var outbox = MemoryEventOutboxRecord.CreateAcknowledgement(MemoryEventOutboxRecordId.New(), ProviderId,
            inbox.ProviderEventId, inbox.InboxRecordId, Now, MemoryPayload.FromJson(JsonSerializer.SerializeToElement(new { accepted = true })));
        var source = new MemorySourceIngestionJobRecord(Guid.NewGuid(), ProviderId,
            new MemorySourceGatewayRequest(CanDoItAll.Memory.SourceGateway.MemorySourceKind.WorkbenchProjectStructure,
                Guid.NewGuid(), MemorySourceScope.Project, Cursor: null, Take: null,
                MemorySourceGatewayPolicy.Allow([CanDoItAll.Memory.SourceGateway.MemorySourceKind.WorkbenchProjectStructure]),
                RequesterId: requester.RequesterId),
            MemorySourceIngestionJobStatus.Queued, CreatedAtUtc: Now, UpdatedAtUtc: Now, StatusReason: "queued");
        return new SavedRecords(profile, operation, feedback, inbox, outbox, source);
    }

    private sealed record SavedRecords(
        MemoryProviderProfile Profile,
        MemoryOperationRecord Operation,
        MemoryFeedbackRecord Feedback,
        MemoryEventInboxRecord Inbox,
        MemoryEventOutboxRecord Outbox,
        MemorySourceIngestionJobRecord SourceRequest);
}
