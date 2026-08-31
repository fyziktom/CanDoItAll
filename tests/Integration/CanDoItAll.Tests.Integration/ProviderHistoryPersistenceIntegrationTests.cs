using CanDoItAll.Composition;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedProviders.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Tests.Integration;

public sealed class ProviderHistoryPersistenceIntegrationTests {
    [Fact]
    public async Task ExpiredOrphanInput_IsDeletedAfterFinalAttempt() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = fixture.Start(await DetailedAsync(fixture));
        await fixture.Capture.BeginAsync(start, new("input", 0), default);
        await fixture.Capture.CompleteAsync(start, fixture.Completion(), "response", default);
        fixture.Clock.Now += TimeSpan.FromDays(31);
        var retention = new HistoryRetentionStore(fixture.Factory, fixture.Clock);
        Assert.Equal(2, await retention.PurgeExpiredDetailAsync(fixture.Partition, 10, default));
        Assert.Equal(1, await retention.PurgeExpiredMetadataAsync(fixture.Partition, 1, default));
        Assert.Equal(1, await retention.PurgeExpiredMetadataAsync(fixture.Partition, 1, default));
        Assert.Equal(0, await retention.PurgeExpiredMetadataAsync(fixture.Partition, 1, default));
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Empty(await db.Set<HistoryDetailRow>().ToArrayAsync());
        Assert.Empty(await db.Set<HistoryEntryRow>().ToArrayAsync());
        Assert.Equal(0, (await db.Set<HistoryPolicyRow>().SingleAsync()).UsedDetailBytes);
    }

    [Fact]
    public async Task RetainedRetry_PreservesSharedInput() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var first = fixture.Start(await DetailedAsync(fixture));
        await fixture.Capture.BeginAsync(first, new("input", 0), default);
        await fixture.Capture.CompleteAsync(first, fixture.Completion(), "response", default);
        fixture.Clock.Now += TimeSpan.FromDays(5);
        var retry = first with { EntryId = HistoryEntryId.New(), AttemptId = ProviderAttemptId.New(), StartedAtUtc = fixture.Clock.Now };
        await fixture.Capture.BeginAsync(retry, new("input", 0), default);
        await fixture.Capture.CompleteAsync(retry, fixture.Completion(), "retry", default);
        fixture.Clock.Now += TimeSpan.FromDays(26);
        var retention = new HistoryRetentionStore(fixture.Factory, fixture.Clock);
        await retention.PurgeExpiredDetailAsync(fixture.Partition, 10, default);
        Assert.Equal(1, await retention.PurgeExpiredMetadataAsync(fixture.Partition, 10, default));
        await using var db = fixture.Factory.CreateDbContext();
        var retained = await db.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(retry.EntryId.Value, retained.Id);
        Assert.True(await db.Set<HistoryDetailRow>().AnyAsync(row => row.Id == retained.InputDetailId));
        fixture.Clock.Now += TimeSpan.FromDays(5);
        Assert.Equal(2, await retention.PurgeExpiredMetadataAsync(fixture.Partition, 10, default));
        Assert.Empty(await db.Set<HistoryDetailRow>().AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task ConcurrentInputAttachmentAndCleanup_PreservesReferences() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var first = fixture.Start(await DetailedAsync(fixture));
        await fixture.Capture.BeginAsync(first, new("input", 0), default);
        await fixture.Capture.CompleteAsync(first, fixture.Completion(), "response", default);
        fixture.Clock.Now += TimeSpan.FromDays(31);
        var retention = new HistoryRetentionStore(fixture.Factory, fixture.Clock);
        await retention.PurgeExpiredDetailAsync(fixture.Partition, 10, default);
        await retention.PurgeExpiredMetadataAsync(fixture.Partition, 1, default);
        var retry = first with { EntryId = HistoryEntryId.New(), AttemptId = ProviderAttemptId.New(), StartedAtUtc = fixture.Clock.Now,
            InputExpiresAtUtc = fixture.Clock.Now.AddDays(7) };
        await Task.WhenAll(
            retention.PurgeExpiredMetadataAsync(fixture.Partition, 10, default),
            fixture.Capture.BeginAsync(retry, new("input", 0), default));
        await using var db = fixture.Factory.CreateDbContext();
        var entry = await db.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(HistoryOutcome.Started, entry.Outcome);
        Assert.True(await db.Set<HistoryDetailRow>().AnyAsync(row => row.Id == entry.InputDetailId));
        Assert.Equal(await db.Set<HistoryDetailRow>().SumAsync(row => (long)row.StoredBytes),
            (await db.Set<HistoryPolicyRow>().SingleAsync()).UsedDetailBytes);
    }

    [Fact]
    public async Task Cleanup_IsBoundedAndPartitionIsolated() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var policy = await DetailedAsync(fixture);
        for (var index = 0; index < 3; index++) {
            var start = fixture.Start(policy);
            await fixture.Capture.BeginAsync(start, new("input", 0), default);
            await fixture.Capture.CompleteAsync(start, fixture.Completion(), "response", default);
        }
        fixture.Clock.Now += TimeSpan.FromDays(31);
        var retention = new HistoryRetentionStore(fixture.Factory, fixture.Clock);
        await Assert.ThrowsAsync<ProviderHistoryException>(() => retention.PurgeExpiredMetadataAsync(
            fixture.Partition with { StorageLineageId = Guid.NewGuid() }, 1, default));
        for (var pass = 0; pass < 10; pass++) {
            Assert.InRange(await retention.PurgeExpiredDetailAsync(fixture.Partition, 1, default), 0, 1);
            Assert.InRange(await retention.PurgeExpiredMetadataAsync(fixture.Partition, 1, default), 0, 1);
        }
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Empty(await db.Set<HistoryEntryRow>().ToArrayAsync());
        Assert.Empty(await db.Set<HistoryDetailRow>().ToArrayAsync());
    }

    [Fact]
    public async Task Sub_microsecond_timestamps_preserve_attempt_identity_and_terminal_idempotence() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        fixture.Clock.Now = fixture.Clock.Now.AddTicks(7);
        var start = fixture.Start();
        var completion = fixture.Completion();
        await fixture.Capture.BeginAsync(start, null, default);
        await fixture.Capture.BeginAsync(start, null, default);
        await fixture.Capture.CompleteAsync(start, completion, null, default);
        await fixture.Capture.CompleteAsync(start, completion, null, default);
        await using var db = fixture.Factory.CreateDbContext();
        var row = await db.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(start.StartedAtUtc.AddTicks(-7), row.StartedAtUtc);
        Assert.Equal(completion.FinishedAtUtc.AddTicks(-7), row.FinishedAtUtc);
        Assert.Equal(HistoryOutcome.Succeeded, row.Outcome);
    }

    [Fact]
    public async Task Partition_identity_is_stable_under_concurrent_bootstrap() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var partitions = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => new HistoryPartitionStore(fixture.Factory).GetAsync(default)));
        Assert.All(partitions, partition => Assert.Equal(fixture.Partition, partition));
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Single(await db.Set<HistoryPartitionRow>().ToListAsync());
    }

    [Fact]
    public async Task Light_capture_persists_metadata_without_prompt_or_response() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = fixture.Start();
        await fixture.Capture.BeginAsync(start, new("private fixture input", 0), default);
        await fixture.Capture.CompleteAsync(start, fixture.Completion(), "private fixture response", default);
        await using var db = fixture.Factory.CreateDbContext();
        var entry = await db.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(HistoryOutcome.Succeeded, entry.Outcome);
        Assert.Equal(start.Caller.CredentialId!.Value.Value, entry.CredentialId);
        Assert.Equal(0.01m, entry.Amount);
        Assert.Equal(HistoryDetailState.NotCaptured, entry.DetailState);
        Assert.Empty(await db.Set<HistoryDetailRow>().ToListAsync());
    }

    [Fact]
    public async Task Retry_input_is_stored_once_and_responses_are_per_attempt() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var policy = await DetailedAsync(fixture);
        var first = fixture.Start(policy);
        await fixture.Capture.BeginAsync(first, new("input fixture-secret-token", 2), default);
        await fixture.Capture.CompleteAsync(first, fixture.Completion(), "first response", default);
        fixture.Clock.Now += TimeSpan.FromDays(1);
        var second = first with { EntryId = HistoryEntryId.New(), AttemptId = ProviderAttemptId.New(), StartedAtUtc = fixture.Clock.Now };
        await fixture.Capture.BeginAsync(second, new("input fixture-secret-token", 2), default);
        await fixture.Capture.CompleteAsync(second, fixture.Completion(), "second response", default);
        await using var db = fixture.Factory.CreateDbContext();
        var bodies = await db.Set<HistoryDetailRow>().ToListAsync();
        Assert.Equal(3, bodies.Count);
        var input = Assert.Single(bodies, row => row.Part == HistoryDetailPart.Input);
        Assert.Equal(first.StartedAtUtc.AddDays(7), input.ExpiresAtUtc);
        Assert.DoesNotContain("fixture-secret-token", input.ProtectedText, StringComparison.Ordinal);
        var entry = await db.Set<HistoryEntryRow>().SingleAsync(row => row.Id == second.EntryId.Value);
        var detail = await fixture.Details.ReadAsync(db, entry, default);
        Assert.Equal("input [redacted]", detail.Input!.Text);
        Assert.Equal("second response", detail.Response!.Text);
        Assert.Equal(HistoryDetailFlags.Redacted | HistoryDetailFlags.PriorContextNotCaptured, detail.Input.Flags);
    }

    [Fact]
    public async Task Quota_is_atomic_across_captures() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var policy = await DetailedAsync(fixture, quota: 600);
        var starts = Enumerable.Range(0, 12).Select(_ => fixture.Start(policy)).ToArray();
        await Task.WhenAll(starts.Select(start => fixture.Capture.BeginAsync(start, new(new string('x', 32), 0), default)));
        await using var db = fixture.Factory.CreateDbContext();
        var row = await db.Set<HistoryPolicyRow>().SingleAsync();
        Assert.InRange(row.UsedDetailBytes, 1, 600);
        Assert.Equal(row.UsedDetailBytes, await db.Set<HistoryDetailRow>().SumAsync(detail => (long)detail.StoredBytes));
        Assert.Equal(12, await db.Set<HistoryEntryRow>().CountAsync());
        Assert.True(await db.Set<HistoryEntryRow>().AnyAsync(entry => entry.DetailState == HistoryDetailState.QuotaExceeded));
    }

    [Fact]
    public async Task Expired_detail_is_unreadable_before_cleanup() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = fixture.Start(await DetailedAsync(fixture));
        await fixture.Capture.BeginAsync(start, new("private input", 0), default);
        fixture.Clock.Now += TimeSpan.FromDays(8);
        await using var db = fixture.Factory.CreateDbContext();
        var entry = await db.Set<HistoryEntryRow>().SingleAsync();
        var detail = await fixture.Details.ReadAsync(db, entry, default);
        Assert.Equal(HistoryDetailState.Expired, detail.State);
        Assert.Null(detail.Input);
        Assert.Single(await db.Set<HistoryDetailRow>().ToListAsync());
    }

    [Fact]
    public async Task Profile_switch_does_not_redirect_finalization() {
        await using var original = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var other = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = original.Start();
        await original.Capture.BeginAsync(start, null, default);
        original.Runtime.Generation++;
        await original.Capture.CompleteAsync(start, original.Completion(), null, default);
        await Assert.ThrowsAsync<ProviderHistoryException>(() => original.Capture.BeginAsync(start, null, default));
        await Assert.ThrowsAsync<ProviderHistoryException>(() => other.Capture.CompleteAsync(start, original.Completion(), null, default));
        await using var db = original.Factory.CreateDbContext();
        Assert.Equal(HistoryOutcome.Succeeded, (await db.Set<HistoryEntryRow>().SingleAsync()).Outcome);
        await using var otherDb = other.Factory.CreateDbContext();
        Assert.Empty(await otherDb.Set<HistoryEntryRow>().ToListAsync());
    }

    [Fact]
    public async Task Late_cancellation_does_not_erase_observed_usage() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = fixture.Start();
        await fixture.Capture.BeginAsync(start, null, default);
        await fixture.Capture.CompleteAsync(start, fixture.Completion(), null, default);
        await fixture.Capture.CompleteAsync(start, new(HistoryOutcome.Cancelled, fixture.Clock.Now.AddSeconds(2),
            new(HistoryUsageState.Unavailable), new(HistoryPriceState.Unpriced)), null, default);
        await using var db = fixture.Factory.CreateDbContext();
        var entry = await db.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(HistoryOutcome.Succeeded, entry.Outcome);
        Assert.Equal(10, entry.InputTokens);
        Assert.Equal(0.01m, entry.Amount);
    }

    [Fact]
    public async Task Canonical_owner_never_gets_a_duplicate_detail_body() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = fixture.Start(await DetailedAsync(fixture)) with {
            ContentOwner = new(fixture.Partition, HistorySourceKind.SimpleChat, new("chat"), new("turn"))
        };
        await fixture.Capture.BeginAsync(start, new("already owned input", 0), default);
        await fixture.Capture.CompleteAsync(start, fixture.Completion(), "already owned response", default);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Empty(await db.Set<HistoryDetailRow>().ToListAsync());
        Assert.Equal(HistoryOwnerState.PendingCanonical, (await db.Set<HistoryOwnerRow>().SingleAsync()).State);
    }

    [Fact]
    public async Task Policy_requires_authorization_version_and_explicit_shortening() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var initial = await DetailedAsync(fixture);
        var start = fixture.Start(initial);
        await fixture.Capture.BeginAsync(start, new("input", 0), default);
        var shorter = initial.Policy with { DetailRetentionDays = 1, MetadataRetentionDays = 2 };
        var changed = await fixture.Policy.UpdateAsync(new(shorter, initial.Version, false), default);
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.Equal(start.StartedAtUtc.AddDays(7), (await db.Set<HistoryDetailRow>().SingleAsync()).ExpiresAtUtc);
        }
        await fixture.Policy.UpdateAsync(new(shorter, changed.Version, true), default);
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.Equal(start.StartedAtUtc.AddDays(1), (await db.Set<HistoryDetailRow>().SingleAsync()).ExpiresAtUtc);
            Assert.Equal(start.StartedAtUtc.AddDays(2), (await db.Set<HistoryEntryRow>().SingleAsync()).ExpiresAtUtc);
            Assert.Equal(3, await db.Set<HistoryPolicyAuditRow>().CountAsync());
        }
        await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Policy.UpdateAsync(new(shorter, initial.Version, false), default));
        fixture.Access.Denied = true;
        await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Policy.GetAsync(default));
    }

    [Fact]
    public async Task ReviewedHeadToRepairs_PreservesSharingHistoryAndTransfer() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync(migrate: true);
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync(migrate: true);
        var start = source.Start(await DetailedAsync(source));
        await source.Capture.BeginAsync(start, new("retained input", 7), default);
        await source.Capture.CompleteAsync(start, source.Completion(), "retained response", default);
        await source.Projection.ApplyAsync(new(new(source.Partition, HistorySourceKind.Workflow, new("workflow"), new("node")),
            new(1), HistorySourceMutationKind.Upsert, null, [start.EntryId]) { Role = HistoryOwnerRole.Lineage }, default);
        var mutation = new HistorySourceMutation(new(source.Partition, HistorySourceKind.SimpleChat, new("chat"), new("turn")),
            new(1), HistorySourceMutationKind.Upsert, null, [HistoryEntryId.New()]);
        await using (var ownerDb = source.Factory.CreateDbContext()) {
            source.Outbox.Stage(ownerDb, mutation);
            await ownerDb.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ProviderHistoryException>(() => source.Processor.ProcessAsync(source.Partition, 50, default));
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        var context = new DatabaseTransferContext(Profile("source"), Profile("target"), sourceDb, targetDb, true);
        var local = new CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile {
            Name = "Preserved publisher", ConnectorPluginKey = ProviderConnectorKeys.OpenAi,
            ConfigSchemaVersion = "1.0", DefaultModel = "preserved", BaseUrl = "https://example.invalid/v1"
        };
        var publication = SharedProviderPublicationTransitions.Create(local.Id, SharedProviderPublicationId.New(), source.Clock.Now);
        SharedProviderPublicationTransitions.Publish(publication, source.Clock.Now);
        var secret = new SecretRecord {
            Id = Guid.NewGuid(), Name = "Fixture source token", Kind = SecretKind.ApiKey,
            EncryptedPayload = "fixture-not-a-credential", CreatedAtUtc = source.Clock.Now, UpdatedAtUtc = source.Clock.Now
        };
        var remote = new SharedProviderSource {
            Name = "Preserved source", BaseUri = "https://example.invalid/",
            ApiTokenSecretId = secret.Id, RemoteInstanceId = SharedProviderSourceInstanceId.New(),
            CreatedAtUtc = source.Clock.Now, UpdatedAtUtc = source.Clock.Now
        };
        var imported = new CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile {
            Name = "Preserved import", ConnectorPluginKey = ProviderConnectorKeys.OpenAi,
            ConfigSchemaVersion = "1.0", DefaultModel = SharedProviderApiTestData.RoutingModelId.Value
        };
        var remotePublication = SharedProviderApiTestData.Catalog.Providers[0];
        var import = new SharedProviderImport {
            SourceId = remote.Id, ProviderProfileId = imported.Id,
            RemotePublicationId = remotePublication.PublicationId, RemoteRevision = remotePublication.Revision,
            RemoteDefaultModelId = remotePublication.DefaultModelId, RemotePurpose = remotePublication.Purpose,
            RemoteTransport = remotePublication.Transport, RemoteDisplayName = remotePublication.DisplayName,
            RemoteCatalogSnapshotJson = System.Text.Json.JsonSerializer.Serialize(remotePublication),
            CreatedAtUtc = source.Clock.Now, UpdatedAtUtc = source.Clock.Now
        };
        sourceDb.AddRange(local, publication, secret, remote, imported, import);
        await sourceDb.SaveChangesAsync();
        var appliedBefore = (await sourceDb.Database.GetAppliedMigrationsAsync()).ToArray();
        Assert.Equal("20260830104752_AddProviderHistoryExternalReference", appliedBefore.Last());
        await sourceDb.Database.GetService<IMigrator>().MigrateAsync();
        Assert.Equal(appliedBefore, await sourceDb.Database.GetAppliedMigrationsAsync());
        sourceDb.ChangeTracker.Clear();
        Assert.Equal(publication.PublicId, (await sourceDb.Set<ProviderSharePublication>().SingleAsync()).PublicId);
        Assert.Equal(remote.RemoteInstanceId, (await sourceDb.Set<SharedProviderSource>().SingleAsync()).RemoteInstanceId);
        var preservedImport = await sourceDb.Set<SharedProviderImport>().SingleAsync();
        Assert.Equal(import.SourceId, preservedImport.SourceId);
        Assert.Equal(import.RemoteRevision, preservedImport.RemoteRevision);
        Assert.Equal(import.RemoteCatalogSnapshotJson, preservedImport.RemoteCatalogSnapshotJson);
        var blocked = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new AiProvidersDatabaseTransferHandler([new SharedProviderDatabaseTransferGuard()]).TransferAsync(context));
        Assert.Contains("transfer is blocked", blocked.Message);
        Assert.Empty(await targetDb.Set<CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile>().ToArrayAsync());
        var locator = new AgentHistoryLocator {
            PartitionId = source.Partition.StorageLineageId, EvidenceId = Guid.NewGuid(), OwnerId = Guid.NewGuid(),
            ScopeKind = WorkspaceScopeKind.Organization, ScopeKey = "original-profile", SourceVersion = 8, IsDeleted = true
        };
        sourceDb.Add(locator);
        await sourceDb.SaveChangesAsync();
        var result = await new HistoryDatabaseTransferHandler([new AgentHistoryTransferParticipant()]).TransferAsync(context);
        var copiedLocator = await targetDb.Set<AgentHistoryLocator>().SingleAsync();
        Assert.Equal(locator.EvidenceId, copiedLocator.EvidenceId);
        Assert.Equal(locator.OwnerId, copiedLocator.OwnerId);
        Assert.Equal("original-profile", copiedLocator.ScopeKey);
        Assert.Equal(8, copiedLocator.SourceVersion);
        Assert.True(copiedLocator.IsDeleted);
        Assert.True(result.Success);
        Assert.Equal(source.Partition, await new HistoryPartitionStore(target.Factory).GetAsync(default));
        var policy = await targetDb.Set<HistoryPolicyRow>().AsNoTracking().SingleAsync();
        Assert.Equal(1, policy.Version);
        Assert.Equal(policy.UsedDetailBytes, await targetDb.Set<HistoryDetailRow>().SumAsync(row => (long)row.StoredBytes));
        var outbox = await targetDb.Set<HistoryOutboxRow>().SingleAsync();
        Assert.Equal(mutation.Source, outbox.Mutation.Source);
        Assert.Equal(1, outbox.Attempts);
        Assert.Equal(HistoryCoverageState.Failed,
            (await targetDb.Set<HistoryCheckpointRow>().SingleAsync(row => row.SourceKind == HistorySourceKind.SimpleChat)).Coverage);
        var entry = await targetDb.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(start.EntryId.Value, entry.Id);
        Assert.Equal(start.EntryId.Value, (await targetDb.Set<HistoryOwnerRow>().SingleAsync()).EntryId);
        Assert.Equal(1, (await targetDb.Set<HistorySourceRow>().SingleAsync()).Version);
        Assert.Equal(start.StartedAtUtc.AddDays(30), entry.ExpiresAtUtc);
        Assert.Equal("retained input", (await source.Details.ReadAsync(targetDb, entry, default)).Input!.Text);
        Assert.Equal(HistoryDetailState.ProtectionUnavailable, (await target.Details.ReadAsync(targetDb, entry, default)).State);
    }

    [Fact]
    public async Task History_transfer_rolls_back_when_a_file_owner_project_is_missing() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        var project = Guid.NewGuid();
        sourceDb.Add(new AgentHistoryLocator {
            PartitionId = source.Partition.StorageLineageId, EvidenceId = Guid.NewGuid(), OwnerId = Guid.NewGuid(),
            ScopeKind = WorkspaceScopeKind.Project, ScopeKey = project.ToString("D"), ProjectId = project, SourceVersion = 1
        });
        await sourceDb.SaveChangesAsync();
        var context = new DatabaseTransferContext(Profile("source"), Profile("target"), sourceDb, targetDb, true);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new HistoryDatabaseTransferHandler([new AgentHistoryTransferParticipant()]).TransferAsync(context));
        await using var verification = target.Factory.CreateDbContext();
        Assert.Equal(target.Partition, await new HistoryPartitionStore(target.Factory).GetAsync(default));
        Assert.Empty(await verification.Set<AgentHistoryLocator>().ToListAsync());
    }

    [Fact]
    public async Task History_transfer_refuses_to_replace_retained_target_history() {
        await using var source = await HistoryPersistenceTestDatabase.CreateAsync();
        await using var target = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = target.Start();
        await target.Capture.BeginAsync(start, null, default);
        await using var sourceDb = source.Factory.CreateDbContext();
        await using var targetDb = target.Factory.CreateDbContext();
        var context = new DatabaseTransferContext(Profile("source"), Profile("target"), sourceDb, targetDb, true);
        var handler = new HistoryDatabaseTransferHandler([]);
        Assert.False((await handler.PreviewAsync(context)).IsAvailable);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.TransferAsync(context));
        Assert.Equal(start.EntryId.Value, (await targetDb.Set<HistoryEntryRow>().SingleAsync()).Id);
    }

    private static ResolvedDatabaseProfile Profile(string name) => new(
        new DatabaseProfileRecord { DisplayName = name }, DatabaseProfileResolutionSource.ExplicitOverride, name);

    [Fact]
    public async Task Retention_preview_is_bounded_and_oversized_apply_rolls_back_atomically() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var initial = await DetailedAsync(fixture);
        for (var index = 0; index < 3; index++) {
            await fixture.Capture.BeginAsync(fixture.Start(initial), new("bounded input", 0), default);
        }
        var shorter = initial.Policy with { MetadataRetentionDays = 2, DetailRetentionDays = 1, BatchSize = 2 };
        var preview = await fixture.Policy.PreviewShorterRetentionAsync(shorter, default);
        Assert.Equal(new HistoryRetentionPreview(3, 3, 2, true), preview);
        var failure = await Assert.ThrowsAsync<ProviderHistoryException>(() =>
            fixture.Policy.UpdateAsync(new(shorter, initial.Version, true), default));
        Assert.Equal(HistoryFailure.InvalidQuery, failure.Failure);
        Assert.Equal(initial, await fixture.Policy.GetAsync(default));
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.All(await db.Set<HistoryEntryRow>().ToArrayAsync(), row => Assert.Equal(row.SortAtUtc.AddDays(30), row.ExpiresAtUtc));
            Assert.All(await db.Set<HistoryDetailRow>().ToArrayAsync(), row => Assert.Equal(row.CapturedAtUtc.AddDays(7), row.ExpiresAtUtc));
            Assert.Single(await db.Set<HistoryPolicyAuditRow>().ToArrayAsync());
        }
        var futureOnly = await fixture.Policy.UpdateAsync(new(shorter, initial.Version, false), default);
        Assert.Equal(initial.Version + 1, futureOnly.Version);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authority_or_profile_change_after_policy_flush_rolls_back(bool profileChanged) {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var initial = await fixture.Policy.GetAsync(default);
        var originalContext = fixture.Access.Context;
        var factory = fixture.Factory.WithInterceptor(new AfterPolicyFlush(() => {
            if (profileChanged) {
                fixture.Runtime.Generation++;
                fixture.Access.Context = originalContext with { Fence = new(fixture.Runtime.Generation, 0) };
            } else {
                fixture.Access.Denied = true;
            }
        }));
        var policy = new HistoryPolicyStore(factory, fixture.Access, fixture.Clock,
            new(fixture.Access, fixture.Reads, fixture.Clock, Microsoft.Extensions.Logging.Abstractions.NullLogger<HistoryAuthorizedOperation>.Instance),
            fixture.Runtime, fixture.Runtime);
        var failure = await Assert.ThrowsAsync<ProviderHistoryException>(() =>
            policy.UpdateAsync(new(initial.Policy with { MetadataRetentionDays = 20 }, initial.Version, false), default));
        Assert.Equal(profileChanged ? HistoryFailure.StaleContext : HistoryFailure.Denied, failure.Failure);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Equal(initial.Version, (await db.Set<HistoryPolicyRow>().SingleAsync()).Version);
        Assert.Empty(await db.Set<HistoryPolicyAuditRow>().ToArrayAsync());
    }

    [Fact]
    public async Task Concurrent_policy_editors_cannot_both_commit_the_same_version() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var initial = await fixture.Policy.GetAsync(default);
        async Task<HistoryFailure?> UpdateAsync(int days) {
            try {
                await fixture.Policy.UpdateAsync(new(initial.Policy with { MetadataRetentionDays = days }, initial.Version, false), default);
                return null;
            } catch (ProviderHistoryException exception) {
                return exception.Failure;
            }
        }
        var results = await Task.WhenAll(UpdateAsync(20), UpdateAsync(25));
        Assert.Single(results, result => result is null);
        Assert.Single(results, result => result == HistoryFailure.Conflict);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Single(await db.Set<HistoryPolicyAuditRow>().ToArrayAsync());
    }

    private sealed class AfterPolicyFlush(Action action) : Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesInterceptor {
        public override ValueTask<int> SavedChangesAsync(Microsoft.EntityFrameworkCore.Diagnostics.SaveChangesCompletedEventData eventData,
            int result, CancellationToken cancellationToken = default) {
            action();
            return ValueTask.FromResult(result);
        }
    }

    private static Task<HistoryPolicySnapshot> DetailedAsync(HistoryPersistenceTestDatabase fixture, long quota = 1024 * 1024)
        => fixture.Policy.UpdateAsync(new(new() { CaptureMode = HistoryCaptureMode.Detailed, DetailQuotaBytes = quota }, 0, false), default);

    [Fact]
    public void Production_model_contains_history_index_owner_outbox_and_policy() {
        AppDbContextModelRegistry.ConfigureAssemblies(ModuleAssemblies.All);
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=history-model;Username=postgres").Options;
        using var db = new AppDbContext(options);
        var tables = db.Model.GetEntityTypes().Select(entity => entity.GetTableName()).ToHashSet();
        Assert.Contains("ProviderHistory_Entries", tables);
        Assert.Contains("ProviderHistory_Owners", tables);
        Assert.Contains("ProviderHistory_Outbox", tables);
        Assert.Contains("ProviderHistory_Policies", tables);
    }
}
