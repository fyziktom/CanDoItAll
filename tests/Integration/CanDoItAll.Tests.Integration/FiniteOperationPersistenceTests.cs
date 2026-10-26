using System.Data;
using System.Data.Common;
using System.Collections.Concurrent;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class FiniteOperationPersistenceTests {
    [Fact]
    public async Task Matching_active_readiness_and_admission_refuse_missing_or_inconsistent_issued_metadata() {
        await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
        await using var environment = CanDoItAllTestEnvironment.Create("finite-readiness-missing");
        var files = DatabaseTransferTestSupport.CreateActivationService(environment);
        var coordinator = new DatasetActivationCoordinator(files, [fixture.Owner.Pool, fixture.Pool]);
        Assert.Equal(fixture.Binding, await coordinator.AttachAsync(fixture.Owner.Profile));
        await using (var edit = fixture.Owner.Context()) {
            await edit.Set<FiniteOperationStateRecord>().Where(row => row.PoolId == fixture.Pool.Pool.Value).ExecuteDeleteAsync();
        }
        foreach (var operation in new Func<Task<DatasetActivationBinding>>[] {
            () => coordinator.EnsureReadyAsync(fixture.Owner.Profile),
            () => coordinator.AttachAsync(fixture.Owner.Profile),
            () => coordinator.ResumeAsync(fixture.Owner.Profile)
        }) {
            var failure = await Assert.ThrowsAsync<FiniteOperationProtocolException>(operation);
            Assert.Equal(FiniteOperationProtocolFailure.InconsistentMetadata, failure.Failure);
        }
        await using (var owner = fixture.Owner.Context()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            Assert.Equal(FiniteOperationProtocolFailure.MissingState,
                (await Assert.ThrowsAsync<FiniteOperationProtocolException>(() =>
                    fixture.Owner.Service.EnterAsync(owner, fixture.Binding, fixture.Pool, 1, CancellationToken.None))).Failure);
        }
        await using var read = fixture.Owner.Context();
        Assert.False(await read.Set<FiniteOperationStateRecord>().AnyAsync(row => row.PoolId == fixture.Pool.Pool.Value));
        Assert.True(await read.Set<FiniteOperationEpochRecord>().AnyAsync(row => row.PoolId == fixture.Pool.Pool.Value));
        Assert.Equal(fixture.Binding, await files.ReadActiveBindingAsync(fixture.Owner.Profile.Profile.Id));
        var now = await fixture.Owner.NowAsync();
        read.Add(new FiniteOperationStateRecord {
            NamespaceId = fixture.Binding.Namespace.Value, PoolId = fixture.Pool.Pool.Value,
            RetentionProtocolVersion = 1, CurrentEpoch = 2, RetiredThroughEpoch = 0
        });
        read.Add(new FiniteOperationEpochRecord {
            NamespaceId = fixture.Binding.Namespace.Value, PoolId = fixture.Pool.Pool.Value, Epoch = 2,
            State = FiniteOperationEpochState.Closed, OpenedAtUtc = now.AddHours(-1), AdmissionClosesAtUtc = now.AddHours(1),
            ClosedAtUtc = now, MutationDeadlineMilliseconds = 30000, ReplayRetentionSeconds = 604800
        });
        await read.SaveChangesAsync();
        await using (var owner = fixture.Owner.Context()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            Assert.Equal(FiniteOperationProtocolFailure.InconsistentMetadata,
                (await Assert.ThrowsAsync<FiniteOperationProtocolException>(() =>
                    fixture.Owner.Service.EnterAsync(owner, fixture.Binding, fixture.Pool, 1, CancellationToken.None))).Failure);
        }
        await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => coordinator.EnsureReadyAsync(fixture.Owner.Profile));
        await read.Set<FiniteOperationStateRecord>().Where(row => row.PoolId == fixture.Pool.Pool.Value)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.RetentionProtocolVersion, 2));
        Assert.Equal(FiniteOperationProtocolFailure.UnsupportedProtocol,
            (await Assert.ThrowsAsync<FiniteOperationProtocolException>(() =>
                fixture.Owner.Service.ReadCurrentAsync(fixture.Binding, fixture.Pool, CancellationToken.None))).Failure);
        await read.Set<FiniteOperationStateRecord>().Where(row => row.PoolId == fixture.Pool.Pool.Value)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.RetentionProtocolVersion, 1));
        await read.Set<FiniteOperationEpochRecord>().Where(row => row.PoolId == fixture.Pool.Pool.Value && row.Epoch == 2).ExecuteDeleteAsync();
        Assert.Equal(FiniteOperationProtocolFailure.MissingRetainedEpoch,
            (await Assert.ThrowsAsync<FiniteOperationProtocolException>(() => coordinator.ResumeAsync(fixture.Owner.Profile))).Failure);
    }

    [Fact]
    public async Task Enlisted_multi_pool_reactivation_rolls_back_as_one_unit_and_repeat_keeps_original_issued_values() {
        await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
        var replacement = new DatasetActivationBinding(new(Guid.NewGuid()), Guid.NewGuid(),
            fixture.Binding.SchemaVersion, fixture.Binding.GuardProtocol);
        var plan = new DatasetActivationPlan(Guid.NewGuid(), DatasetActivationKind.Reactivate, replacement, fixture.Binding);
        await ApplyAsync(commit: false);
        Assert.Equal(fixture.Binding, (await fixture.Owner.LifecycleStore.ReadAsync())!.Binding);
        await using (var read = fixture.Owner.Context()) {
            Assert.False(await read.Set<FiniteOperationStateRecord>().AnyAsync(row => row.NamespaceId == replacement.Namespace.Value));
            Assert.All(await read.Set<FiniteOperationEpochRecord>().ToArrayAsync(), row => Assert.Equal(FiniteOperationEpochState.Open, row.State));
        }
        await ApplyAsync(commit: true);
        var original = await fixture.Owner.Service.ReadCurrentAsync(replacement, fixture.Pool, CancellationToken.None);
        await ApplyAsync(commit: true);
        var repeated = await fixture.Owner.Service.ReadCurrentAsync(replacement, fixture.Pool, CancellationToken.None);
        Assert.Equal(original.Binding, repeated.Binding);
        Assert.Equal(original.Epoch, repeated.Epoch);
        Assert.Equal(original.OpenedAtUtc, repeated.OpenedAtUtc);
        Assert.Equal(original.AdmissionClosesAtUtc, repeated.AdmissionClosesAtUtc);
        Assert.Equal(original.MutationDeadlineMilliseconds, repeated.MutationDeadlineMilliseconds);
        Assert.Equal(original.ReplayRetentionSeconds, repeated.ReplayRetentionSeconds);

        async Task ApplyAsync(bool commit) {
            await using var owner = fixture.Owner.Context();
            await using var transaction = await owner.Database.BeginTransactionAsync();
            using (fixture.Owner.Transactions.Enter(owner)) {
                await fixture.Owner.LifecycleStore.StageActivationAsync(plan);
                foreach (var pool in new[] { fixture.Owner.Pool, fixture.Pool }) {
                    await fixture.Owner.Service.CloseForReactivationEnlistedAsync(fixture.Binding, replacement, pool, CancellationToken.None);
                    await fixture.Owner.Service.InitializeNewNamespaceEnlistedAsync(replacement, pool, CancellationToken.None);
                }
            }
            if (commit) {
                await transaction.CommitAsync();
            }
        }
    }

    [Fact]
    public async Task Explicit_new_pool_initialization_is_required_and_later_options_do_not_rewrite_its_promise() {
        await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
        var added = new FiniteOperationPoolRegistration(new(Guid.NewGuid()), 1, fixture.Pool.Timing, fixture.Pool.Families);
        await using (var owner = fixture.Owner.Context()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            using var participation = fixture.Owner.Transactions.Enter(owner);
            var missing = await Assert.ThrowsAsync<FiniteOperationProtocolException>(() =>
                fixture.Owner.Service.RequireReadyEnlistedAsync(fixture.Binding, added, CancellationToken.None));
            Assert.Equal(FiniteOperationProtocolFailure.MissingState, missing.Failure);
        }
        await fixture.Owner.InitializeAsync(fixture.Owner.Service, added);
        var issued = await fixture.Owner.Service.ReadCurrentAsync(fixture.Binding, added, CancellationToken.None);
        var timing = new FiniteOperationTiming(TimeSpan.FromHours(1), TimeSpan.FromHours(1), TimeSpan.FromSeconds(1),
            TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(5), TimeSpan.FromMinutes(1));
        var tightened = new FiniteOperationPoolRegistration(added.Pool, 1, timing, added.Families);
        await fixture.Owner.InitializeAsync(fixture.Owner.Service, tightened);
        var after = await fixture.Owner.Service.ReadCurrentAsync(fixture.Binding, tightened, CancellationToken.None);
        Assert.Equal(issued.AdmissionClosesAtUtc, after.AdmissionClosesAtUtc);
        Assert.Equal(issued.MutationDeadlineMilliseconds, after.MutationDeadlineMilliseconds);
        Assert.Equal(issued.ReplayRetentionSeconds, after.ReplayRetentionSeconds);
        Assert.Equal(issued.ReplayUntilUtc, after.ReplayUntilUtc);
    }

    [Fact]
    public async Task Other_pool_admission_and_rotation_do_not_wait_for_a_locked_pool_state() {
        await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
        await using var held = fixture.Owner.Context();
        await using var transaction = await held.Database.BeginTransactionAsync();
        await held.Database.ExecuteSqlInterpolatedAsync($"""
            SELECT "CurrentEpoch" FROM "Infrastructure_FiniteOperationState"
            WHERE "NamespaceId"={fixture.Binding.Namespace.Value} AND "PoolId"={fixture.Pool.Pool.Value} FOR UPDATE
            """);
        await fixture.Owner.SetDeadlineAsync(TimeSpan.FromSeconds(-1));
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        Assert.Equal(FiniteOperationRotationResult.Rotated,
            await fixture.Owner.Service.RotateExpiredAsync(fixture.Binding, fixture.Owner.Pool, deadline.Token));
        await using var owner = fixture.Owner.Context();
        await using var owning = await owner.Database.BeginTransactionAsync(deadline.Token);
        var admitted = await fixture.Owner.Service.EnterAsync(owner, fixture.Binding, fixture.Owner.Pool, 2, deadline.Token);
        Assert.Equal(fixture.Owner.Pool.Pool, admitted.Pool);
        Assert.Equal(2, admitted.Epoch);
        Assert.Equal(FiniteOperationRetirementStatus.Busy,
            (await fixture.Owner.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, deadline.Token)).Status);
    }

    [Fact]
    public async Task Nonempty_or_failed_family_inspection_retains_metadata_until_every_required_family_is_empty() {
        await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
        await fixture.RetireFirstAsync();
        await fixture.Second.SeedAsync(1);
        Assert.Equal(0, (await fixture.Maintenance().CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None)).DeletedEpochs);
        Assert.True(await fixture.HasFirstEpochAsync());
        await fixture.Second.ClearAsync();
        fixture.Second.AfterInspection = _ => throw new InvalidOperationException("Synthetic unknown family inspection.");
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Maintenance().CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None));
        Assert.True(await fixture.HasFirstEpochAsync());
        fixture.Second.AfterInspection = null;
        Assert.Equal(1, (await fixture.Maintenance().CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None)).DeletedEpochs);
        Assert.False(await fixture.HasFirstEpochAsync());
        Assert.Equal(3, fixture.First.TransactionIds.Count);
        Assert.Equal(3, fixture.Second.TransactionIds.Count);
        Assert.Equal(6, fixture.First.TransactionIds.Concat(fixture.Second.TransactionIds).Distinct().Count());
    }

    [Fact]
    public async Task Exclusive_import_table_order_cannot_invert_separate_family_inspection_transactions() {
        await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
        await fixture.RetireFirstAsync();
        var reversed = new FiniteOperationPoolRegistration(fixture.Pool.Pool, fixture.Pool.ProtocolVersion,
            fixture.Pool.Timing, fixture.Pool.Families.Reverse().ToArray());
        var maintenance = new FiniteOperationReceiptMaintenance(fixture.Owner.CreateNeutralOptions(), fixture.Owner.Transactions,
            [reversed], [fixture.First, fixture.Second]);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var inspected = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Second.AfterInspection = async token => {
            inspected.TrySetResult();
            await release.Task.WaitAsync(token);
        };
        var cleanup = maintenance.CleanupMetadataAsync(fixture.Binding, reversed, deadline.Token);
        await using var importer = fixture.Owner.Context();
        Task? import = null;
        try {
            await inspected.Task.WaitAsync(deadline.Token);
            await using var transaction = await importer.Database.BeginTransactionAsync(IsolationLevel.Serializable, deadline.Token);
            await importer.Database.ExecuteSqlRawAsync("LOCK TABLE \"Infrastructure_DatasetLifecycle\" IN EXCLUSIVE MODE; LOCK TABLE \"FiniteTest_First\" IN ACCESS EXCLUSIVE MODE", deadline.Token);
            import = importer.Database.ExecuteSqlRawAsync("LOCK TABLE \"FiniteTest_Second\" IN ACCESS EXCLUSIVE MODE", deadline.Token);
            var pid = ((NpgsqlConnection)importer.Database.GetDbConnection()).ProcessID;
            await WaitBlockedAsync(fixture.Owner, pid, deadline.Token);
            release.TrySetResult();
            await import;
            await DatasetLifecycleStore.RequireActiveForMaintenanceAsync(importer, fixture.Binding, deadline.Token);
            await transaction.CommitAsync(deadline.Token);
        } finally {
            release.TrySetResult();
            try {
                if (import is not null) {
                    await import;
                }
            } finally {
                await cleanup;
            }
        }
        Assert.Equal(1, (await cleanup).DeletedEpochs);
        Assert.False(await fixture.HasFirstEpochAsync());
    }

    [Fact]
    public async Task Concurrent_metadata_workers_remove_each_empty_closed_epoch_once_and_preserve_open_state() {
        await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
        await fixture.RetireFirstAsync();
        var results = await Task.WhenAll(
            fixture.Maintenance().CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None),
            fixture.Maintenance().CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None));
        Assert.Equal(1, results.Sum(result => result.DeletedEpochs));
        Assert.Equal(0, results.Sum(result => result.DeletedStates));
        await using var read = fixture.Owner.Context();
        var current = await read.Set<FiniteOperationEpochRecord>().SingleAsync(row => row.PoolId == fixture.Pool.Pool.Value);
        Assert.Equal(2, current.Epoch);
        Assert.Equal(FiniteOperationEpochState.Open, current.State);
        Assert.Equal(1, (await read.Set<FiniteOperationStateRecord>().SingleAsync(row => row.PoolId == fixture.Pool.Pool.Value)).RetiredThroughEpoch);
        read.AddRange(Enumerable.Range(0, FiniteOperationBounds.BatchRows + 1).Select(_ => new FiniteOperationStateRecord {
            NamespaceId = Guid.NewGuid(), PoolId = fixture.Pool.Pool.Value, RetentionProtocolVersion = fixture.Pool.ProtocolVersion,
            CurrentEpoch = 1, RetiredThroughEpoch = 0
        }));
        await read.SaveChangesAsync();
        var firstStates = await fixture.Maintenance().CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None);
        Assert.Equal(FiniteOperationBounds.BatchRows, firstStates.DeletedStates);
        Assert.Equal(0, firstStates.DeletedEpochs);
        Assert.True(firstStates.MoreEligibleMetadata);
        var lastState = await fixture.Maintenance().CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None);
        Assert.Equal(1, lastState.DeletedStates);
        Assert.False(lastState.MoreEligibleMetadata);
        var now = await fixture.Owner.NowAsync();
        await fixture.Owner.SeedHistoryAsync([new FiniteOperationEpochRecord {
            NamespaceId = fixture.Binding.Namespace.Value, PoolId = fixture.Owner.Pool.Pool.Value, Epoch = 1,
            State = FiniteOperationEpochState.Closed, OpenedAtUtc = now.AddDays(-4), AdmissionClosesAtUtc = now.AddDays(-3),
            ClosedAtUtc = now.AddDays(-2), MutationDeadlineMilliseconds = 1000, ReplayRetentionSeconds = 60
        }], 1);
        await fixture.Owner.SeedReceiptsAsync(1, 2, 3);
        await using var projectContext = fixture.Owner.CreateDbContext();
        var barrier = new ConcurrentPurgeEntry();
        var options = new DbContextOptionsBuilder<ProjectsDbContext>()
            .UseNpgsql(projectContext.Database.GetConnectionString()).AddInterceptors(barrier).Options;
        var participant = new ProjectDefinitionReceiptRetention(options, fixture.Owner.Transactions);
        var shared = new FiniteOperationReceiptMaintenance(fixture.Owner.CreateNeutralOptions(), fixture.Owner.Transactions,
            [fixture.Owner.Pool], [participant, new ProjectLifecycleReceiptRetention(options, fixture.Owner.Transactions)]);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var purges = await Task.WhenAll(
            shared.PurgeRetiredBatchAsync(fixture.Binding, fixture.Owner.Pool, participant.Family, deadline.Token),
            shared.PurgeRetiredBatchAsync(fixture.Binding, fixture.Owner.Pool, participant.Family, deadline.Token));
        Assert.Equal(2, barrier.Transactions.Count);
        Assert.Equal(2, barrier.Transactions.Distinct().Count());
        Assert.Equal(2, purges.Sum(result => result.DeletedReceipts));
        Assert.Equal(6, purges.Sum(result => result.DeletedBytes));
        Assert.Empty(await projectContext.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().ToArrayAsync());
    }

    [Fact]
    public async Task Binding_or_protocol_change_before_final_metadata_delete_rolls_back_and_keeps_the_epoch() {
        foreach (var changeBinding in new[] { false, true }) {
            await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
            await fixture.RetireFirstAsync();
            var gate = new BeforeMetadataDelete(async () => {
                await using var edit = fixture.Owner.Context();
                if (changeBinding) {
                    await edit.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"Infrastructure_DatasetLifecycle\" SET \"NamespaceId\"={Guid.NewGuid()} WHERE \"Id\"=1");
                } else {
                    await edit.Set<FiniteOperationStateRecord>().Where(row => row.PoolId == fixture.Pool.Pool.Value)
                        .ExecuteUpdateAsync(update => update.SetProperty(row => row.RetentionProtocolVersion, 2));
                }
            });
            var failure = await Record.ExceptionAsync(() => fixture.Maintenance(gate).CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None));
            Assert.True(gate.Entered);
            if (changeBinding) {
                Assert.IsType<DatasetActivationMismatchException>(failure);
            } else {
                Assert.Equal(FiniteOperationProtocolFailure.UnsupportedProtocol, Assert.IsType<FiniteOperationProtocolException>(failure).Failure);
            }
            Assert.True(await fixture.HasFirstEpochAsync());
        }
    }

    [Fact]
    public async Task Closed_epoch_cannot_admit_a_late_receipt_between_empty_inspection_and_metadata_delete() {
        await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
        await fixture.RetireFirstAsync();
        var attempts = 0;
        fixture.First.AfterInspection = async token => {
            attempts++;
            await using var owner = fixture.Owner.Context();
            await using var transaction = await owner.Database.BeginTransactionAsync(token);
            var refusal = await Assert.ThrowsAsync<FiniteOperationAdmissionException>(() =>
                fixture.Owner.Service.EnterAsync(owner, fixture.Binding, fixture.Pool, 1, token));
            Assert.Equal(FiniteOperationAdmissionFailure.RetiredEpoch, refusal.Failure);
        };
        Assert.Equal(1, (await fixture.Maintenance().CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None)).DeletedEpochs);
        Assert.Equal(1, attempts);
        Assert.Equal(0, await fixture.First.CountAsync());
        Assert.False(await fixture.HasFirstEpochAsync());
    }

    [Fact]
    public async Task A_failed_family_does_not_hide_peer_progress_or_claim_empty_after_later_metadata_failure() {
        await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
        await fixture.RetireFirstAsync();
        await fixture.First.SeedAsync(1);
        await fixture.Second.SeedAsync(1);
        fixture.First.BeforePurge = _ => throw new InvalidOperationException("Synthetic first-family failure.");
        fixture.Second.AfterInspection = _ => throw new InvalidOperationException("Synthetic later metadata failure.");
        var result = await fixture.Cycle().RunAsync(fixture.Owner.Service, fixture.Maintenance(), fixture.Binding, CancellationToken.None);
        Assert.Equal(FiniteOperationMaintenanceStop.Failed, result.Stop);
        Assert.Equal(1, result.DeletedReceipts);
        Assert.Equal(3, result.DeletedBytes);
        Assert.Equal(FiniteOperationEligibleWork.Unknown, result.EligibleWork);
        Assert.Equal(1, await fixture.First.CountAsync());
        Assert.Equal(0, await fixture.Second.CountAsync());
        Assert.True(await fixture.HasFirstEpochAsync());
        Assert.Equal(1, result.Families.Single(family => family.Family == fixture.Second.Family).DeletedReceipts);
    }

    [Fact]
    public async Task Family_cursor_survives_budget_exhaustion_and_resumes_with_the_unserved_peer() {
        await using var fixture = await FiniteOperationPersistenceFixture.CreateAsync();
        await fixture.RetireFirstAsync();
        await fixture.First.SeedAsync(129);
        await fixture.Second.SeedAsync(1);
        using var budget = new CancellationTokenSource();
        var cancellation = new CancelAfterFirstPurge(budget);
        var first = await fixture.Cycle().RunAsync(fixture.Owner.CreateService(cancellation), fixture.Maintenance(cancellation), fixture.Binding, budget.Token);
        Assert.Equal(FiniteOperationMaintenanceStop.Budget, first.Stop);
        Assert.Equal(128, first.DeletedReceipts);
        Assert.Equal(384, first.DeletedBytes);
        var order = new List<FiniteReceiptFamilyId>();
        fixture.First.BeforePurge = _ => {
            order.Add(fixture.First.Family);
            return Task.CompletedTask;
        };
        fixture.Second.BeforePurge = _ => {
            order.Add(fixture.Second.Family);
            return Task.CompletedTask;
        };
        var next = await fixture.Cycle().RunAsync(fixture.Owner.Service, fixture.Maintenance(), fixture.Binding, CancellationToken.None);
        Assert.Equal(fixture.Second.Family, order[0]);
        Assert.Equal(2, next.DeletedReceipts);
        Assert.Equal(FiniteOperationMaintenanceStop.Empty, next.Stop);
        Assert.False(await fixture.HasFirstEpochAsync());
    }

    [Fact]
    public async Task Original_lookup_snapshot_keeps_receipt_bytes_across_retirement_and_purge_and_never_invents_absence() {
        await using var fixture = await ProjectOperationEpochTests.Fixture.CreateAsync();
        await fixture.SeedReceiptsAsync(1, 1, 3);
        var now = await fixture.NowAsync();
        await fixture.SeedHistoryAsync([new FiniteOperationEpochRecord {
            NamespaceId = fixture.Binding.Namespace.Value, PoolId = fixture.Pool.Pool.Value, Epoch = 1, State = FiniteOperationEpochState.Closed,
            OpenedAtUtc = now.AddDays(-4), AdmissionClosesAtUtc = now.AddDays(-3), ClosedAtUtc = now.AddDays(-2),
            MutationDeadlineMilliseconds = 1000, ReplayRetentionSeconds = 60
        }], 0);
        await using var original = fixture.CreateDbContext();
        var receipt = await original.Set<ProjectDefinitionReceiptRecord>().AsNoTracking().SingleAsync();
        var caller = ProjectMutationCallerScope.LocalWorkspace;
        var operation = new ProjectDefinitionOperation(fixture.Binding.Namespace, new(1), receipt.OperationId, caller.Bind(fixture.Binding.Namespace));
        var barrier = new ReceiptReadBarrier();
        await using var snapshot = new ProjectsDbContext(new DbContextOptionsBuilder<ProjectsDbContext>()
            .UseNpgsql(original.Database.GetConnectionString()).AddInterceptors(barrier).Options);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var reading = ProjectDefinitionOperationStore.ObserveAsync(snapshot, operation, caller, deadline.Token);
        try {
            await barrier.Entered.Task.WaitAsync(deadline.Token);
            await fixture.Service.AdvanceRetirementAsync(fixture.Binding, fixture.Pool, deadline.Token);
            Assert.Equal(1, (await fixture.PurgeAsync(cancellationToken: deadline.Token)).DeletedReceipts);
            await fixture.CleanupMetadataAsync(cancellationToken: deadline.Token);
        } finally {
            barrier.Release.TrySetResult();
        }
        var observed = (await reading)!;
        Assert.Equal(0, observed.RetiredThroughEpoch);
        Assert.Equal(receipt.AcceptedPayload, observed.AcceptedPayload);
        await using var environment = CanDoItAllTestEnvironment.Create("finite-lookup-retirement");
        var reader = fixture.CreateReceiptReader(DatabaseTransferTestSupport.CreateActivationService(environment));
        var lookup = await reader.ObserveAsync(fixture.Binding, operation, new ProjectDefinitionReceiptInvocation(caller, new NeverDisclose()), deadline.Token);
        Assert.IsType<ProjectDefinitionLookup.Expired>(lookup.Lookup);
    }

    private static async Task WaitBlockedAsync(ProjectOperationEpochTests.Fixture fixture, int pid, CancellationToken token) {
        await using var observer = fixture.Context();
        while (!await observer.Database.SqlQuery<bool>($"SELECT cardinality(pg_blocking_pids({pid}))>0 AS \"Value\"").SingleAsync(token)) {
            await Task.Delay(TimeSpan.FromMilliseconds(10), token);
        }
    }

    private sealed class BeforeMetadataDelete(Func<Task> action) : DbCommandInterceptor {
        internal bool Entered { get; private set; }
        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (!Entered && command.CommandText.StartsWith("DELETE FROM \"Infrastructure_FiniteOperationEpochs\"", StringComparison.Ordinal)) {
                Entered = true;
                await action();
            }
            return result;
        }
    }

    private sealed class CancelAfterFirstPurge(CancellationTokenSource cancellation) : DbConnectionInterceptor {
        private int disposals;
        public override Task ConnectionDisposedAsync(DbConnection connection, ConnectionEndEventData eventData) {
            if (++disposals == 3) {
                cancellation.Cancel();
            }
            return Task.CompletedTask;
        }
    }

    private sealed class ReceiptReadBarrier : DbCommandInterceptor {
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class ConcurrentPurgeEntry : DbCommandInterceptor {
        internal ConcurrentBag<DbTransaction> Transactions { get; } = [];
        private readonly TaskCompletionSource bothEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int entries;

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("WITH cleanup_epoch AS", StringComparison.Ordinal)) {
                Transactions.Add(Assert.IsAssignableFrom<DbTransaction>(command.Transaction));
                if (Interlocked.Increment(ref entries) == 2) {
                    bothEntered.TrySetResult();
                }
                await bothEntered.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }

    private sealed class NeverDisclose : IProjectDefinitionReceiptAccess {
        public Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken)
            => throw new InvalidOperationException("Retired evidence must not invoke target disclosure.");
    }
}
