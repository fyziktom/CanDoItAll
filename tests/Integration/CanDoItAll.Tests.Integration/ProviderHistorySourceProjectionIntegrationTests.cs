using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.ProviderHistory.Persistence;
using CanDoItAll.Modules.Workspace;
using Microsoft.EntityFrameworkCore;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace CanDoItAll.Tests.Integration;

public sealed class ProviderHistorySourceProjectionIntegrationTests {
    [Fact]
    public async Task Source_and_intent_commit_atomically() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var mutation = Mutation(fixture);
        await using (var ownerDb = fixture.Factory.CreateDbContext()) {
            await using var transaction = await ownerDb.Database.BeginTransactionAsync();
            ownerDb.Add(new WorkspaceSettings { DefaultProviderProfileId = Guid.NewGuid(), UpdatedAtUtc = fixture.Clock.Now });
            fixture.Outbox.Stage(ownerDb, mutation);
            await ownerDb.SaveChangesAsync();
            await transaction.RollbackAsync();
        }
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.Empty(await db.Set<WorkspaceSettings>().ToListAsync());
            Assert.Empty(await db.Set<HistoryOutboxRow>().ToListAsync());
        }
        await using (var ownerDb = fixture.Factory.CreateDbContext()) {
            ownerDb.Add(new WorkspaceSettings { DefaultProviderProfileId = Guid.NewGuid(), UpdatedAtUtc = fixture.Clock.Now });
            fixture.Outbox.Stage(ownerDb, mutation);
            await ownerDb.SaveChangesAsync();
        }
        Assert.Equal(1, await fixture.Processor.ProcessAsync(fixture.Partition, 50, default));
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.Single(await db.Set<WorkspaceSettings>().ToListAsync());
            Assert.Empty(await db.Set<HistoryOutboxRow>().ToListAsync());
            Assert.Equal(mutation.Entry!.Id.Value, (await db.Set<HistoryEntryRow>().SingleAsync()).Id);
            Assert.Equal(HistoryOwnerState.Linked, (await db.Set<HistoryOwnerRow>().SingleAsync()).State);
        }
    }

    [Fact]
    public async Task Tombstone_blocks_stale_replay_and_sort_time_is_immutable() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var mutation = Mutation(fixture);
        await fixture.Projection.ApplyAsync(mutation, default);
        await fixture.Projection.ApplyAsync(mutation with {
            Version = new(2), Entry = mutation.Entry! with { SortAtUtc = fixture.Clock.Now.AddDays(2), Version = 2 }
        }, default);
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.Equal(mutation.Entry!.SortAtUtc, (await db.Set<HistoryEntryRow>().SingleAsync()).SortAtUtc);
        }
        await fixture.Projection.ApplyAsync(mutation with { Version = new(3), Kind = HistorySourceMutationKind.Delete, Entry = null }, default);
        await fixture.Projection.ApplyAsync(mutation, default);
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.False((await db.Set<HistoryEntryRow>().SingleAsync()).IsVisible);
            Assert.Equal(3, (await db.Set<HistorySourceRow>().SingleAsync()).Version);
            Assert.Equal(HistoryOwnerState.Deleted, (await db.Set<HistoryOwnerRow>().SingleAsync()).State);
        }
    }

    [Fact]
    public async Task Same_source_version_with_different_payload_is_rejected() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var mutation = Mutation(fixture);
        await fixture.Projection.ApplyAsync(mutation, default);
        var failure = await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Projection.ApplyAsync(
            mutation with { Entry = mutation.Entry! with { Outcome = HistoryOutcome.Failed } }, default));
        Assert.Equal(HistoryFailure.Conflict, failure.Failure);
    }

    [Fact]
    public async Task Aggregate_lineage_does_not_add_a_second_usage_row() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = fixture.Start();
        await fixture.Capture.BeginAsync(start, null, default);
        await fixture.Capture.CompleteAsync(start, fixture.Completion(), null, default);
        var mutation = Mutation(fixture) with { LinkedEntries = [start.EntryId] };
        await fixture.Projection.ApplyAsync(mutation, default);
        await using var db = fixture.Factory.CreateDbContext();
        var entry = await db.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(start.EntryId.Value, entry.Id);
        Assert.Equal(0.01m, entry.Amount);
        Assert.Equal(HistoryOwnerRole.Lineage, (await db.Set<HistoryOwnerRow>().SingleAsync()).Role);
    }

    [Fact]
    public async Task Late_canonical_commit_retains_metadata_without_reviving_expired_input() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var policy = await fixture.Policy.UpdateAsync(new(new() { CaptureMode = HistoryCaptureMode.Detailed }, 0, false), default);
        var start = fixture.Start(policy);
        await fixture.Capture.BeginAsync(start, new("old input", 0), default);
        await fixture.Capture.CompleteAsync(start, fixture.Completion(), "old response", default);
        fixture.Clock.Now += TimeSpan.FromDays(40);
        var mutation = Mutation(fixture) with { Entry = null, LinkedEntries = [start.EntryId] };
        await fixture.Projection.ApplyAsync(mutation, default);
        await using var db = fixture.Factory.CreateDbContext();
        var entry = await db.Set<HistoryEntryRow>().SingleAsync();
        Assert.Equal(HistoryRetentionAuthority.CanonicalOwner, entry.RetentionAuthority);
        Assert.Null(entry.ExpiresAtUtc);
        Assert.Equal(HistoryDetailState.Expired, (await fixture.Details.ReadAsync(db, entry, default)).State);
    }

    [Fact]
    public async Task Deleted_owner_does_not_hide_another_retained_owner() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var first = Mutation(fixture);
        await fixture.Projection.ApplyAsync(first, default);
        var other = first with { Source = first.Source with { Owner = new("other-owner") } };
        await fixture.Projection.ApplyAsync(other, default);
        await fixture.Projection.ApplyAsync(first with { Kind = HistorySourceMutationKind.Delete, Version = new(2), Entry = null }, default);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.True((await db.Set<HistoryEntryRow>().SingleAsync()).IsVisible);
        Assert.Equal(1, await db.Set<HistoryOwnerRow>().CountAsync(row => row.State == HistoryOwnerState.Linked));
    }

    [Fact]
    public async Task Failed_projection_remains_queued_with_actionable_failure() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var mutation = Mutation(fixture) with { Entry = null, LinkedEntries = [HistoryEntryId.New()] };
        await using (var db = fixture.Factory.CreateDbContext()) {
            fixture.Outbox.Stage(db, mutation);
            await db.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<ProviderHistoryException>(() => fixture.Processor.ProcessAsync(fixture.Partition, 10, default));
        await using (var db = fixture.Factory.CreateDbContext()) {
            var row = await db.Set<HistoryOutboxRow>().SingleAsync();
            Assert.Equal(1, row.Attempts);
            Assert.Equal(nameof(HistoryFailure.Conflict), row.FailureCode);
            Assert.Empty(await db.Set<HistoryEntryRow>().ToListAsync());
        }
    }

    [Fact]
    public async Task Recovery_preserves_live_hosts_and_terminal_evidence_can_reconcile_expired_lease() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var start = fixture.Start();
        await fixture.Capture.BeginAsync(start, null, default);
        var recovery = new HistoryRecoveryStore(fixture.Factory, fixture.Clock);
        Assert.Equal(0, await recovery.InterruptAbandonedAsync(fixture.Partition, 10, default));
        fixture.Clock.Now += TimeSpan.FromSeconds(91);
        Assert.Equal(1, await recovery.InterruptAbandonedAsync(fixture.Partition, 10, default));
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.Equal(HistoryOutcome.Interrupted, (await db.Set<HistoryEntryRow>().SingleAsync()).Outcome);
        }
        await fixture.Capture.CompleteAsync(start, fixture.Completion(), null, default);
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.Equal(HistoryOutcome.Succeeded, (await db.Set<HistoryEntryRow>().SingleAsync()).Outcome);
        }
    }

    [Fact]
    public async Task Registered_hosted_service_consumes_production_outbox_without_manual_projection() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var mutation = Mutation(fixture);
        await using (var db = fixture.Factory.CreateDbContext()) {
            fixture.Outbox.Stage(db, mutation);
            await db.SaveChangesAsync();
        }
        var expired = fixture.Start();
        await fixture.Capture.BeginAsync(expired, null, default);
        await fixture.Capture.CompleteAsync(expired, fixture.Completion(), null, default);
        fixture.Clock.Now += TimeSpan.FromDays(31);
        using var host = new HostBuilder().ConfigureServices(services => {
            services.AddSingleton<IDbContextFactory<AppDbContext>>(fixture.Factory);
            services.AddSingleton<TimeProvider>(fixture.Clock);
            services.AddSingleton<IDatabaseRuntimeState>(fixture.Runtime);
            services.AddSingleton<IDatabaseRuntimeWriteFence>(fixture.Runtime);
            services.AddProviderHistoryPersistence();
        }).Build();
        await host.StartAsync();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        try {
            while (true) {
                await using var db = fixture.Factory.CreateDbContext();
                if (await db.Set<HistoryEntryRow>().AnyAsync(row => row.Id == mutation.Entry!.Id.Value, deadline.Token) &&
                    !await db.Set<HistoryEntryRow>().AnyAsync(row => row.Id == expired.EntryId.Value, deadline.Token)) {
                    Assert.Empty(await db.Set<HistoryOutboxRow>().ToListAsync(deadline.Token));
                    break;
                }
                await Task.Delay(50, deadline.Token);
            }
        } finally {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Registered_worker_does_not_hold_runtime_fence_over_source_io_and_continues_after_source_failure() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var runtime = new DatabaseRuntimeState(new DatabaseSwitchNotificationService());
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var broken = new MaintenanceSource((_, _, _, _) => throw new InvalidDataException("broken source"),
            HistorySourceKind.AgentConversation);
        var fileIo = new MaintenanceSource(async (context, _, _, token) => {
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return new("file-complete", true);
        });
        using var host = new HostBuilder().ConfigureServices(services => {
            services.AddSingleton<IDbContextFactory<AppDbContext>>(fixture.Factory);
            services.AddSingleton<TimeProvider>(fixture.Clock);
            services.AddSingleton<IDatabaseRuntimeState>(runtime);
            services.AddSingleton<IDatabaseRuntimeWriteFence>(runtime);
            services.AddSingleton<IHistorySourceMaintenance>(broken);
            services.AddSingleton<IHistorySourceMaintenance>(fileIo);
            services.AddProviderHistoryPersistence();
        }).Build();
        await host.StartAsync();
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(await runtime.ExecuteAsync(runtime.GetSnapshot(), _ => Task.FromResult(true))
                .WaitAsync(TimeSpan.FromSeconds(1)));
            await using var db = fixture.Factory.CreateDbContext();
            var failure = await db.Set<HistoryCheckpointRow>().SingleAsync(row => row.SourceKind == broken.Kind);
            Assert.Equal(HistoryCoverageState.Failed, failure.Coverage);
            Assert.Equal(nameof(InvalidDataException), failure.FailureCode);
        } finally {
            release.TrySetResult();
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task Purged_input_does_not_reappear_on_late_retry_and_started_rows_are_not_deleted() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var policy = await fixture.Policy.UpdateAsync(new(new() { CaptureMode = HistoryCaptureMode.Detailed }, 0, false), default);
        var start = fixture.Start(policy);
        await fixture.Capture.BeginAsync(start, new("retained only seven days", 0), default);
        var retention = new HistoryRetentionStore(fixture.Factory, fixture.Clock);
        fixture.Clock.Now += TimeSpan.FromDays(40);
        Assert.Equal(1, await retention.PurgeExpiredDetailAsync(fixture.Partition, 500, default));
        Assert.Equal(0, await retention.PurgeExpiredMetadataAsync(fixture.Partition, 500, default));
        await fixture.Capture.CompleteAsync(start, fixture.Completion(), "late expired response", default);
        Assert.Equal(1, await retention.PurgeExpiredMetadataAsync(fixture.Partition, 500, default));
        var retry = start with { EntryId = HistoryEntryId.New(), AttemptId = ProviderAttemptId.New(), StartedAtUtc = fixture.Clock.Now };
        await fixture.Capture.BeginAsync(retry, new("retained only seven days", 0), default);
        await using var db = fixture.Factory.CreateDbContext();
        var input = await db.Set<HistoryDetailRow>().SingleAsync();
        Assert.Empty(input.ProtectedText);
        Assert.Equal(0, input.StoredBytes);
        Assert.Equal(start.StartedAtUtc.AddDays(7), input.ExpiresAtUtc);
        Assert.Equal(HistoryDetailState.Expired, (await db.Set<HistoryEntryRow>().SingleAsync()).DetailState);
        Assert.Equal(0, (await db.Set<HistoryPolicyRow>().SingleAsync()).UsedDetailBytes);
    }

    [Fact]
    public async Task Deleting_last_content_owner_keeps_primary_metadata_but_removes_content_availability() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var content = Mutation(fixture);
        await fixture.Projection.ApplyAsync(content, default);
        await fixture.Projection.ApplyAsync(content with {
            Source = content.Source with { Kind = HistorySourceKind.Workflow, Owner = new("workflow") },
            Role = HistoryOwnerRole.PrimaryEvidence
        }, default);
        await fixture.Projection.ApplyAsync(content with {
            Kind = HistorySourceMutationKind.Delete, Version = new(2), Entry = null
        }, default);
        await using var db = fixture.Factory.CreateDbContext();
        var retained = await db.Set<HistoryEntryRow>().SingleAsync();
        Assert.True(retained.IsVisible);
        Assert.Equal(HistoryDetailState.Unavailable, retained.DetailState);
    }

    [Fact]
    public async Task Source_checkpoint_advances_only_after_durable_work_and_replay_is_idempotent() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var runner = new HistorySourceMaintenanceRunner(fixture.Factory, fixture.Clock);
        var mutation = Mutation(fixture);
        var fail = true;
        var source = new MaintenanceSource(async (partition, cursor, maximum, token) => {
            Assert.Null(cursor);
            Assert.Equal(2, maximum);
            await using var db = fixture.Factory.CreateDbContext();
            fixture.Outbox.Stage(db, mutation);
            await db.SaveChangesAsync(token);
            if (fail) {
                throw new InvalidOperationException("Crash after durable work, before checkpoint.");
            }
            return new("finished", true);
        });
        await Assert.ThrowsAsync<InvalidOperationException>(() => runner.ProcessAsync(source, fixture.Maintenance, 2, default));
        await using (var db = fixture.Factory.CreateDbContext()) {
            var checkpoint = await db.Set<HistoryCheckpointRow>().SingleAsync(row => row.SourceKind == source.Kind);
            Assert.Null(checkpoint.Cursor);
            Assert.Null(checkpoint.LeaseOwner);
            Assert.Equal(HistoryCoverageState.Failed, checkpoint.Coverage);
            Assert.Equal(nameof(InvalidOperationException), checkpoint.FailureCode);
        }
        fail = false;
        Assert.True(await runner.ProcessAsync(source, fixture.Maintenance, 2, default));
        Assert.Equal(2, await fixture.Processor.ProcessAsync(fixture.Partition, 10, default));
        var finished = new MaintenanceSource((partition, cursor, maximum, token) => {
            Assert.Equal("finished", cursor);
            return Task.FromResult(new HistorySourceProgress(cursor, true));
        });
        Assert.True(await runner.ProcessAsync(finished, fixture.Maintenance, 2, default));
        await using (var db = fixture.Factory.CreateDbContext()) {
            Assert.Single(await db.Set<HistoryEntryRow>().ToArrayAsync());
            var checkpoint = await db.Set<HistoryCheckpointRow>().SingleAsync(row => row.SourceKind == source.Kind);
            Assert.Equal(HistoryCoverageState.Current, checkpoint.Coverage);
            Assert.Null(checkpoint.FailureCode);
            Assert.Equal(fixture.Clock.Now, checkpoint.IndexedThroughUtc);
        }
    }

    [Fact]
    public async Task Source_maintenance_lease_excludes_competitors_until_expiry() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var runner = new HistorySourceMaintenanceRunner(fixture.Factory, fixture.Clock);
        await using (var db = fixture.Factory.CreateDbContext()) {
            var checkpoint = await db.Set<HistoryCheckpointRow>().SingleAsync(row => row.SourceKind == HistorySourceKind.SimpleChat);
            checkpoint.LeaseOwner = Guid.NewGuid();
            checkpoint.LeaseUntilUtc = fixture.Clock.Now.AddSeconds(30);
            await db.SaveChangesAsync();
        }
        var calls = 0;
        var source = new MaintenanceSource((partition, cursor, maximum, token) => {
            calls++;
            return Task.FromResult(new HistorySourceProgress(null, true));
        });
        Assert.False(await runner.ProcessAsync(source, fixture.Maintenance, 2, default));
        Assert.Equal(0, calls);
        fixture.Clock.Now += TimeSpan.FromSeconds(31);
        Assert.True(await runner.ProcessAsync(source, fixture.Maintenance, 2, default));
        Assert.Equal(1, calls);
    }

    [Fact]
    public async Task Source_maintenance_rejects_profile_change_before_checkpoint_without_using_the_new_database() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var runner = new HistorySourceMaintenanceRunner(fixture.Factory, fixture.Clock);
        var context = fixture.Maintenance;
        var source = new MaintenanceSource((_, _, _, _) => {
            fixture.Runtime.Generation++;
            return Task.FromResult(new HistorySourceProgress("must-not-advance", true));
        });
        await Assert.ThrowsAsync<DatabaseRuntimeProfileChangedException>(() => runner.ProcessAsync(source, context, 2, default));
        await using var db = fixture.Factory.CreateDbContext();
        var checkpoint = await db.Set<HistoryCheckpointRow>().SingleAsync(row => row.SourceKind == source.Kind);
        Assert.Null(checkpoint.Cursor);
        Assert.NotNull(checkpoint.LeaseOwner);
        Assert.Equal(HistoryCoverageState.Partial, checkpoint.Coverage);
    }

    [Fact]
    public async Task Two_sources_can_publish_the_same_attempt_in_one_owner_transaction() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var first = Mutation(fixture);
        var second = first with { Source = first.Source with { Evidence = new("second-owner") } };
        await using (var owner = fixture.Factory.CreateDbContext()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            await HistoryProjectionWriter.StageAsync(owner, first, default);
            await HistoryProjectionWriter.StageAsync(owner, second, default);
            await owner.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        await using var db = fixture.Factory.CreateDbContext();
        Assert.Single(await db.Set<HistoryEntryRow>().ToListAsync());
        Assert.Equal(2, await db.Set<HistorySourceRow>().CountAsync());
        Assert.Equal(2, await db.Set<HistoryOwnerRow>().CountAsync());
    }

    [Fact]
    public async Task Deleting_two_owners_in_one_transaction_hides_the_last_canonical_entry() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var first = Mutation(fixture);
        var second = first with { Source = first.Source with { Evidence = new("second-owner") } };
        await fixture.Projection.ApplyAsync(first, default);
        await fixture.Projection.ApplyAsync(second, default);
        await using (var owner = fixture.Factory.CreateDbContext()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            await HistoryProjectionWriter.StageAsync(owner, first with { Version = new(2), Kind = HistorySourceMutationKind.Delete, Entry = null }, default);
            await HistoryProjectionWriter.StageAsync(owner, second with { Version = new(2), Kind = HistorySourceMutationKind.Delete, Entry = null }, default);
            await owner.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        await using var db = fixture.Factory.CreateDbContext();
        Assert.False((await db.Set<HistoryEntryRow>().SingleAsync()).IsVisible);
        Assert.All(await db.Set<HistoryOwnerRow>().ToArrayAsync(), owner => Assert.Equal(HistoryOwnerState.Deleted, owner.State));
    }

    [Fact]
    public async Task Replacing_an_owner_in_one_transaction_preserves_the_new_canonical_owner() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var first = Mutation(fixture);
        var second = first with { Source = first.Source with { Evidence = new("replacement-owner") } };
        await fixture.Projection.ApplyAsync(first, default);
        await using (var owner = fixture.Factory.CreateDbContext()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            await HistoryProjectionWriter.StageAsync(owner, second, default);
            await HistoryProjectionWriter.StageAsync(owner, first with { Version = new(2), Kind = HistorySourceMutationKind.Delete, Entry = null }, default);
            await owner.SaveChangesAsync();
            await transaction.CommitAsync();
        }
        await using var db = fixture.Factory.CreateDbContext();
        Assert.True((await db.Set<HistoryEntryRow>().SingleAsync()).IsVisible);
        Assert.Equal(1, await db.Set<HistoryOwnerRow>().CountAsync(owner => owner.State == HistoryOwnerState.Linked));
    }

    [Fact]
    public async Task Relay_source_deletion_hides_metadata_and_stale_replay_cannot_restore_it() {
        await using var fixture = await HistoryPersistenceTestDatabase.CreateAsync();
        var seed = Mutation(fixture);
        var mutation = seed with {
            Source = seed.Source with { Kind = HistorySourceKind.SharedRelay },
            Entry = seed.Entry! with { RetentionAuthority = HistoryRetentionAuthority.HistoryPolicy, ExpiresAtUtc = fixture.Clock.Now.AddDays(1) },
            Role = HistoryOwnerRole.PrimaryEvidence
        };
        await fixture.Projection.ApplyAsync(mutation, default);
        await fixture.Projection.ApplyAsync(mutation with { Version = new(2), Kind = HistorySourceMutationKind.Delete, Entry = null }, default);
        await fixture.Projection.ApplyAsync(mutation, default);
        await using var db = fixture.Factory.CreateDbContext();
        Assert.False((await db.Set<HistoryEntryRow>().SingleAsync()).IsVisible);
        Assert.True((await db.Set<HistorySourceRow>().SingleAsync()).IsDeleted);
    }

    private sealed class MaintenanceSource(
        Func<HistoryMaintenanceContext, string?, int, CancellationToken, Task<HistorySourceProgress>> process,
        HistorySourceKind kind = HistorySourceKind.SimpleChat) : IHistorySourceMaintenance {
        public HistorySourceKind Kind => kind;
        public Task<HistorySourceProgress> ProcessAsync(HistoryMaintenanceContext context, string? cursor, int maximumItems,
            CancellationToken cancellationToken) => process(context, cursor, maximumItems, cancellationToken);
    }

    private static HistorySourceMutation Mutation(HistoryPersistenceTestDatabase fixture) {
        var entry = new HistoryEntry(HistoryEntryId.New(), fixture.Partition, null, null,
            HistoryGranularity.LegacyAggregate, fixture.Clock.Now, HistoryTimeBasis.CanonicalRecorded, null, fixture.Clock.Now,
            new(null, "Legacy provider", "legacy", null, null), HistoryOperation.CompleteChat,
            HistoryWorkload.SimpleChat, HistoryOutcome.Succeeded, new(HistoryAuthenticationKind.Unknown),
            new(HistoryUsageState.Partial, 100, 30), new(HistoryPriceState.Unpriced),
            HistoryMetadataAuthority.CanonicalProjection, HistoryRetentionAuthority.CanonicalOwner, HistoryDetailState.Canonical);
        return new(new(fixture.Partition, HistorySourceKind.SimpleChat, new("fixture-chat"), new("fixture-turn")),
            new(1), HistorySourceMutationKind.Upsert, entry, []);
    }
}
