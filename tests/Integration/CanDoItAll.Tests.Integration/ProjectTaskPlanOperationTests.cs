using System.Data;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanOperationTests {
    [Fact]
    public async Task One_batch_commits_exact_tasks_bindings_and_receipt_without_per_task_reads_or_global_binding_lock() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        await using var blocker = await fixture.ContextAsync();
        await using var held = await blocker.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(blocker,
            [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], CancellationToken.None);
        var total = 0;
        int? selects = null;
        var receiptCount = 0;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        foreach (var count in new[] { 1, 10, 100 }) {
            var command = await fixture.CommandAsync(count, deadline.Token);
            fixture.Probe.Reset();
            var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation, deadline.Token));
            var reads = fixture.Probe.Commands.Count(sql => sql.TrimStart().StartsWith("SELECT", StringComparison.Ordinal));
            Assert.Equal(selects ?? reads, reads);
            selects = reads;
            Assert.False(accepted.Replayed);
            Assert.Equal(count, accepted.Acknowledgement.Tasks.Count);
            await fixture.VerifyAsync(total += count, ++receiptCount, accepted.Acknowledgement);
        }
        Assert.Same(held, blocker.Database.CurrentTransaction);
    }

    [Fact]
    public async Task Concurrent_identical_addresses_return_one_exact_committed_batch() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync();
        using var secondAuthority = new TaskPlanAuthority();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var writes = await Task.WhenAll(fixture.Publisher().PublishAsync(command, fixture.Invocation, deadline.Token),
            fixture.Publisher().PublishAsync(command, new(fixture.Caller, secondAuthority, secondAuthority), deadline.Token));
        var accepted = writes.Select(value => Assert.IsType<ProjectTaskPlanOutcome.Accepted>(value)).ToArray();
        Assert.Single(accepted, value => !value.Replayed);
        Assert.Single(accepted, value => value.Replayed);
        Assert.Equal(ProjectTaskPlanOperationCodec.EncodeAccepted(accepted[0].Acknowledgement),
            ProjectTaskPlanOperationCodec.EncodeAccepted(accepted[1].Acknowledgement));
        await fixture.VerifyAsync(2, 1, accepted[0].Acknowledgement);
    }

    [Fact]
    public async Task Reusing_an_address_for_changed_intent_never_appends_another_batch() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var original = await fixture.CommandAsync();
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(original, fixture.Invocation));
        var changed = new ProjectTaskPlanCommand(original.Operation, original.Basis,
            ProjectTaskPlanProposal.Normalize(new(1, [new("Changed", "", "", 0)])), original.Policy, original.Source);
        Assert.Equal(ProjectTaskPlanRefusal.IdentityConflict,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(changed, fixture.Invocation)).Reason);
        await fixture.VerifyAsync(2, 1, accepted.Acknowledgement);
    }

    [Fact]
    public async Task Same_operation_identifier_in_another_trusted_caller_partition_has_distinct_evidence() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var first = await fixture.CommandAsync(1);
        Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(first, fixture.Invocation));
        var secondCaller = new ProjectTaskPlanCaller(ProjectTaskPlanCallerKind.OrdinaryAgent, Enumerable.Repeat((byte)51, 32).ToArray());
        var refreshed = await fixture.CommandAsync(1);
        var second = new ProjectTaskPlanCommand(new(first.Operation.Namespace, first.Operation.Epoch, first.Operation.Id,
            secondCaller.Bind(first.Operation.Namespace)), refreshed.Basis, refreshed.Proposal, refreshed.Policy, refreshed.Source);
        Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(second,
            new(secondCaller, fixture.Authority, fixture.Authority)));
        await fixture.VerifyAsync(2, 2);
    }

    [Fact]
    public async Task Failure_after_real_flush_rolls_back_both_task_rows_and_receipt() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync();
        fixture.Faults.Save.AfterFlush = async token => {
            await using var enlisted = await fixture.Transactions.CreateEnlistedAsync(
                fixture.Services.GetRequiredService<DbContextOptions<WorkbenchDbContext>>(), static options => new WorkbenchDbContext(options), token);
            Assert.NotNull(enlisted.Database.CurrentTransaction);
            Assert.Equal(2, await enlisted.Set<ProjectObjectRecord>().CountAsync(row => row.ProjectId == fixture.Project.ProjectId, token));
            Assert.Single(await enlisted.Set<ProjectTaskPlanReceiptRecord>().ToArrayAsync(token));
            throw new IOException("Injected failure after actual SaveChanges.");
        };
        Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        await fixture.VerifyAsync(0, 0);
        Assert.Equal(ProjectTaskPlanLookupStatus.Open, (await fixture.LookupAsync(command.Operation)).Status);
    }

    [Fact]
    public async Task Lost_acknowledgement_after_real_commit_recovers_the_original_exact_receipt() {
        foreach (var cleanupConflict in new[] { false, true }) {
            await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
            var command = await fixture.CommandAsync();
            var releases = 0;
            fixture.Authority.OnRelease = () => {
                releases++;
                if (cleanupConflict) {
                    throw new DbUpdateConcurrencyException("Injected conflict during source release after committed work.");
                }
            };
            fixture.Faults.Commit.AfterCommit = () => throw new IOException("Injected lost acknowledgement after provider commit.");
            var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
            Assert.True(accepted.Replayed);
            Assert.True(accepted.CleanupIncomplete);
            Assert.Equal(1, releases);
            await fixture.VerifyAsync(2, 1, accepted.Acknowledgement);
            var retry = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
            Assert.Equal(accepted.Acknowledgement.Tasks, retry.Acknowledgement.Tasks);
            Assert.Equal(1, releases);
            await fixture.VerifyAsync(2, 1, retry.Acknowledgement);
        }
    }

    [Fact]
    public async Task Acknowledged_commit_survives_source_release_failure_without_reclassifying_effect() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync();
        fixture.Authority.ReleaseFails = true;
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        Assert.True(accepted.CleanupIncomplete);
        Assert.False(accepted.Replayed);
        await fixture.VerifyAsync(2, 1, accepted.Acknowledgement);
        fixture.Authority.ReleaseFails = false;
        Assert.Equal(accepted.Acknowledgement.Tasks, (await fixture.LookupAsync(command.Operation)).Accepted!.Tasks);
    }

    [Fact]
    public async Task Lost_commit_acknowledgement_and_revoked_or_unavailable_disclosure_remain_unconfirmed() {
        foreach (var unavailable in new[] { false, true }) {
            await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
            var command = await fixture.CommandAsync();
            var failedReads = 0;
            fixture.Probe.BeforeCommand = (sql, _) => {
                if (sql.CommandText.Contains("AS \"ReceiptPresent\"", StringComparison.Ordinal)) {
                    failedReads++;
                    throw new IOException("Injected initial receipt observation failure.");
                }
                return Task.CompletedTask;
            };
            try {
                var initial = Assert.IsType<ProjectTaskPlanOutcome.Unconfirmed>(
                    await fixture.Publisher().PublishAsync(command, fixture.Invocation));
                Assert.Equal(command.Operation, initial.Operation);
                Assert.Equal(ProjectTaskPlanRefusal.CommitUnconfirmed, initial.Reason);
                Assert.Equal(1, failedReads);
                Assert.Equal(0, fixture.Authority.Acquisitions);
            } finally {
                fixture.Probe.BeforeCommand = null;
            }
            await fixture.VerifyAsync(0, 0);
            fixture.Faults.Commit.AfterCommit = () => {
                fixture.Authority.AllowRead = false;
                fixture.Authority.ReadUnavailable = unavailable;
                throw new IOException("Injected lost commit acknowledgement.");
            };
            Assert.Equal(ProjectTaskPlanRefusal.CommitUnconfirmed,
                Assert.IsType<ProjectTaskPlanOutcome.Unconfirmed>(await fixture.Publisher().PublishAsync(command, fixture.Invocation)).Reason);
            await fixture.VerifyAsync(2, 1);
            var acquisitions = fixture.Authority.Acquisitions;
            var retry = Assert.IsType<ProjectTaskPlanOutcome.Unconfirmed>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
            Assert.Equal(command.Operation, retry.Operation);
            Assert.Equal(unavailable ? ProjectTaskPlanRefusal.CommitUnconfirmed : ProjectTaskPlanRefusal.DetailUnavailable, retry.Reason);
            Assert.Equal(acquisitions, fixture.Authority.Acquisitions);
            await fixture.VerifyAsync(2, 1);
            fixture.Authority.AllowRead = true;
            fixture.Authority.ReadUnavailable = false;
            var recovered = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
            Assert.True(recovered.Replayed);
            await fixture.VerifyAsync(2, 1, recovered.Acknowledgement);
        }
    }

    [Fact]
    public async Task Historical_replay_requires_current_read_but_not_new_write_authority_or_tighter_policy() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync();
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        var acquisitions = fixture.Authority.Acquisitions;
        fixture.Authority.AllowWrite = false;
        var replay = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher(new(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)))
            .PublishAsync(command, fixture.Invocation));
        Assert.Equal(acquisitions, fixture.Authority.Acquisitions);
        Assert.Equal(accepted.Acknowledgement.Tasks, replay.Acknowledgement.Tasks);
        fixture.Authority.AllowRead = false;
        Assert.Equal(ProjectTaskPlanLookupStatus.DetailUnavailable, (await fixture.LookupAsync(command.Operation)).Status);
        await fixture.VerifyAsync(2, 1, accepted.Acknowledgement);
    }

    [Fact]
    public async Task Duplicate_found_after_source_acquisition_releases_sql_and_first_source_before_independent_disclosure() {
        foreach (var failure in Enum.GetValues<DuplicateReadFailure>()) {
            await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
            var command = await fixture.CommandAsync();
            using var duplicateAuthority = new TaskPlanAuthority();
            var beforeAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var continueAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
            duplicateAuthority.BeforeAcquire = async token => {
                beforeAcquire.TrySetResult();
                await continueAcquire.Task.WaitAsync(token);
            };
            var duplicate = fixture.Publisher().PublishAsync(command, new(fixture.Caller, duplicateAuthority, duplicateAuthority), deadline.Token);
            ProjectTaskPlanOutcome.Accepted original;
            Task? queuedWriter = null;
            try {
                await beforeAcquire.Task.WaitAsync(deadline.Token);
                original = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation, deadline.Token));
                duplicateAuthority.ReleaseFails = failure == DuplicateReadFailure.SourceRelease;
                fixture.Probe.BeforeCommand = (sql, token) => {
                    if (sql.CommandText.Contains("FROM \"Workbench_TaskPlanReceipts\" AS", StringComparison.Ordinal) &&
                            !sql.CommandText.Contains("LEFT JOIN", StringComparison.Ordinal)) {
                        Assert.DoesNotContain("AcceptedPayload", sql.CommandText, StringComparison.Ordinal);
                        queuedWriter ??= duplicateAuthority.WriteSourceAsync(() => duplicateAuthority.AllowWrite = false, deadline.Token);
                    }
                    return Task.CompletedTask;
                };
                duplicateAuthority.BeforeRead = async token => {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Transactions.CreateEnlistedAsync(
                        fixture.Services.GetRequiredService<DbContextOptions<WorkbenchDbContext>>(), static options => new WorkbenchDbContext(options), token));
                    Assert.NotNull(queuedWriter);
                    await queuedWriter.WaitAsync(token);
                    if (failure == DuplicateReadFailure.Cancellation) {
                        deadline.Cancel();
                        token.ThrowIfCancellationRequested();
                    }
                };
            } finally {
                continueAcquire.TrySetResult();
            }
            var outcome = await duplicate;
            Assert.NotNull(queuedWriter);
            await queuedWriter.WaitAsync(TimeSpan.FromSeconds(5));
            if (failure == DuplicateReadFailure.None) {
                var replay = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(outcome);
                Assert.True(replay.Replayed);
                Assert.Equal(original.Acknowledgement.Tasks, replay.Acknowledgement.Tasks);
            } else {
                Assert.Equal(failure == DuplicateReadFailure.SourceRelease ? ProjectTaskPlanRefusal.CleanupIncomplete : ProjectTaskPlanRefusal.DetailUnavailable,
                    Assert.IsType<ProjectTaskPlanOutcome.Unconfirmed>(outcome).Reason);
                if (failure == DuplicateReadFailure.SourceRelease) {
                    Assert.Equal(0, duplicateAuthority.Disclosures);
                }
            }
            Assert.Equal(1, duplicateAuthority.Acquisitions);
            fixture.Probe.BeforeCommand = null;
            await fixture.VerifyAsync(2, 1, original.Acknowledgement);
        }
    }

    private enum DuplicateReadFailure { None, SourceRelease, Cancellation }
    [Fact]
    public async Task Admission_deadline_is_rechecked_after_an_actual_project_lock_wait() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync();
        await using var holder = await fixture.ContextAsync();
        await using var transaction = await holder.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(holder, [ProjectMutationScopeKeys.ForProject(fixture.Project.ProjectId)], CancellationToken.None);
        var holderPid = ((NpgsqlConnection)holder.Database.GetDbConnection()).ProcessID;
        await holder.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Infrastructure_FiniteOperationEpochs" SET "AdmissionClosesAtUtc" = clock_timestamp() + interval '3 seconds'
            WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value} AND "Epoch" = {command.Operation.Epoch}
            """);
        // Publish must see the shortened promise, so release only the metadata transaction and reacquire P separately.
        await transaction.CommitAsync();
        await using var held = await holder.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(holder, [ProjectMutationScopeKeys.ForProject(fixture.Project.ProjectId)], CancellationToken.None);
        fixture.Probe.Reset();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var write = fixture.Publisher().PublishAsync(command, fixture.Invocation, deadline.Token);
        try {
            await fixture.WaitForProjectWaiterAsync(holderPid, deadline.Token);
            while (await holder.Database.SqlQuery<bool>($"""
                SELECT clock_timestamp() < "AdmissionClosesAtUtc" AS "Value" FROM "Infrastructure_FiniteOperationEpochs"
                WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value} AND "Epoch" = {command.Operation.Epoch}
                """).SingleAsync(deadline.Token)) {
                await Task.Delay(TimeSpan.FromMilliseconds(10), deadline.Token);
            }
        } finally {
            await held.RollbackAsync(CancellationToken.None);
        }
        Assert.Equal(ProjectTaskPlanRefusal.EpochUnavailable, Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await write).Reason);
        await fixture.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Wrong_namespace_caller_future_epoch_and_corrupt_protocol_never_dispatch_authority() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync();
        Assert.Equal(ProjectTaskPlanLookupStatus.DatasetChanged,
            (await fixture.LookupAsync(new(new(Guid.NewGuid()), 1, command.Operation.Id, command.Operation.CallerBinding))).Status);
        Assert.Equal(ProjectTaskPlanLookupStatus.CallerContextChanged,
            (await fixture.LookupAsync(new(command.Operation.Namespace, 1, command.Operation.Id, new('d', 64)))).Status);
        Assert.Equal(ProjectTaskPlanLookupStatus.InvalidContext,
            (await fixture.LookupAsync(new(command.Operation.Namespace, long.MaxValue, command.Operation.Id, command.Operation.CallerBinding))).Status);
        var future = new ProjectTaskPlanCommand(new(command.Operation.Namespace, long.MaxValue, command.Operation.Id,
            command.Operation.CallerBinding), command.Basis, command.Proposal, command.Policy, command.Source);
        Assert.Equal(ProjectTaskPlanRefusal.EpochUnavailable,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(future, fixture.Invocation)).Reason);
        await using var edit = await fixture.ContextAsync();
        await edit.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Infrastructure_FiniteOperationEpochs"
                ("NamespaceId", "PoolId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc", "ClosedAtUtc", "MutationDeadlineMilliseconds", "ReplayRetentionSeconds")
            VALUES ({fixture.Binding.Namespace.Value}, {fixture.Pool.Pool.Value}, {long.MaxValue}, 2,
                clock_timestamp() - interval '1 hour', clock_timestamp() - interval '1 minute', clock_timestamp(), 30000, 604800)
            """);
        Assert.Equal(ProjectTaskPlanLookupStatus.Unavailable, (await fixture.LookupAsync(future.Operation)).Status);
        Assert.Equal(future.Operation,
            Assert.IsType<ProjectTaskPlanOutcome.Unconfirmed>(await fixture.Publisher().PublishAsync(future, fixture.Invocation)).Operation);
        await edit.Database.ExecuteSqlInterpolatedAsync($"""
            DELETE FROM "Infrastructure_FiniteOperationEpochs"
            WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value} AND "Epoch" = {long.MaxValue}
            """);
        await edit.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Infrastructure_FiniteOperationEpochs" SET "OpenedAtUtc" = clock_timestamp() - interval '1 hour',
                "AdmissionClosesAtUtc" = clock_timestamp() - interval '1 minute'
            WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value} AND "Epoch" = 1
            """);
        Assert.Equal(ProjectTaskPlanLookupStatus.Draining, (await fixture.LookupAsync(command.Operation)).Status);
        await edit.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Infrastructure_FiniteOperationEpochs" SET "State" = 2, "ClosedAtUtc" = clock_timestamp()
            WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value} AND "Epoch" = 1;
            INSERT INTO "Infrastructure_FiniteOperationEpochs"
                ("NamespaceId", "PoolId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc", "ClosedAtUtc", "MutationDeadlineMilliseconds", "ReplayRetentionSeconds")
            VALUES ({fixture.Binding.Namespace.Value}, {fixture.Pool.Pool.Value}, 2, 1, clock_timestamp(), clock_timestamp() + interval '1 day', NULL, 30000, 604800);
            UPDATE "Infrastructure_FiniteOperationState" SET "CurrentEpoch" = 2
            WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value}
            """);
        var closed = await fixture.LookupAsync(command.Operation);
        Assert.Equal(ProjectTaskPlanLookupStatus.ClosedAbsent, closed.Status);
        Assert.NotNull(closed.ClosedAtUtc);
        Assert.True(closed.RetainedUntilUtc > closed.ClosedAtUtc);
        await edit.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Infrastructure_FiniteOperationState" SET "RetentionProtocolVersion" = 2
            WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value}
            """);
        Assert.Equal(ProjectTaskPlanLookupStatus.Unavailable, (await fixture.LookupAsync(command.Operation)).Status);
        var unknown = Assert.IsType<ProjectTaskPlanOutcome.Unconfirmed>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        Assert.Equal(command.Operation, unknown.Operation);
        Assert.Equal(ProjectTaskPlanRefusal.CommitUnconfirmed, unknown.Reason);
        Assert.Equal(0, fixture.Authority.Acquisitions);
        await fixture.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Save_time_validation_failure_leaves_no_receipt_and_no_partial_batch() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync(1);
        var oversized = new ProjectTaskPlanCommand(command.Operation, command.Basis,
            ProjectTaskPlanProposal.Normalize(new(1, [new("Task", new('a', 65_536), "", 1)])), command.Policy, command.Source);
        fixture.Probe.Reset();
        Assert.Equal(ProjectTaskPlanRefusal.InvalidInput,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(oversized, fixture.Invocation)).Reason);
        Assert.DoesNotContain(fixture.Probe.Commands, sql => sql.Contains("INSERT INTO", StringComparison.Ordinal));
        await fixture.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Original_source_generation_and_complete_additional_admissions_are_required_before_effect() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync();
        fixture.Authority.SourceChanged = true;
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(command, fixture.Invocation)).Reason);
        fixture.Authority.SourceChanged = false;
        fixture.Authority.AdditionalProjects = [new(fixture.Project.DatabaseProfileId, fixture.Project.ProjectId, Guid.NewGuid())];
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(command, fixture.Invocation)).Reason);
        fixture.Authority.AdditionalProjects = [];
        foreach (var policy in new[] {
            new DecisionDataPolicy(ReadBasisMode.RunSnapshot, PublicationFreshness.PinnedObservation, OnBasisChange.Fail, command.Policy.Budget),
            new DecisionDataPolicy(ReadBasisMode.StepSnapshot, PublicationFreshness.RequireCurrent, OnBasisChange.Replan,
                new(1, TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(5)))
        }) {
            var unsupported = new ProjectTaskPlanCommand(command.Operation, command.Basis, command.Proposal, policy, command.Source);
            Assert.Equal(ProjectTaskPlanRefusal.PolicyChanged,
                Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(unsupported, fixture.Invocation)).Reason);
        }
        var old = new ProjectTaskPlanSource(command.Source.DecisionId, command.Source.Generation,
            command.Source.DecisionStartedAtUtc.AddHours(-1), command.Source.Kind, command.Source.Version, command.Source.Fingerprint);
        Assert.Equal(ProjectTaskPlanRefusal.BudgetElapsed, Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher()
            .PublishAsync(new(command.Operation, command.Basis, command.Proposal, command.Policy, old), fixture.Invocation)).Reason);
        Assert.Equal(ProjectTaskPlanRefusal.PolicyChanged, Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher(
                new(0, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1)))
            .PublishAsync(command, fixture.Invocation)).Reason);
        fixture.Faults.Save.AfterFlush = _ => {
            fixture.Authority.SourceChanged = true;
            return Task.CompletedTask;
        };
        Assert.Equal(ProjectTaskPlanRefusal.SourceChanged,
            Assert.IsType<ProjectTaskPlanOutcome.Rejected>(await fixture.Publisher().PublishAsync(command, fixture.Invocation)).Reason);
        await fixture.VerifyAsync(0, 0);
    }
}
