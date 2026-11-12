using System.Data;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanRetentionTests {
    [Fact]
    public async Task Composed_owner_has_exact_pool_participant_and_retained_receipt_blocks_business_import() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var registrations = fixture.Services.GetServices<FiniteOperationPoolRegistration>().ToArray();
        Assert.Single(registrations, pool => pool.Pool == WorkbenchTaskPlanFiniteOperations.Pool);
        Assert.Single(fixture.Services.GetServices<IFiniteOperationReceiptRetentionParticipant>(),
            owner => owner.Pool == fixture.Pool.Pool && owner.Family == WorkbenchTaskPlanFiniteOperations.Family);
        Assert.IsType<ProjectTaskPlanPublisher>(fixture.Services.GetRequiredService<ProjectTaskPlanPublisher>());
        Assert.Null(fixture.Services.GetService<IProjectTaskPlanPublicationAuthority>());
        await using var context = await fixture.ContextAsync();
        var entity = context.Model.FindEntityType(typeof(ProjectTaskPlanReceiptRecord))!;
        Assert.Empty(entity.GetForeignKeys());
        Assert.Empty(entity.GetReferencingForeignKeys());
        Assert.Equal(5, entity.FindPrimaryKey()!.Properties.Count);
        var participant = Assert.Single(fixture.Services.GetServices<IProjectTransferTargetStateParticipant>(),
            candidate => candidate.Area == ProjectTransferTargetStateArea.Workbench);
        Assert.Contains(typeof(ProjectTaskPlanReceiptRecord), participant.EntityTypesToLock);
        var profile = fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var operations = fixture.Services.GetRequiredService<DatabaseTransferOperationRunner>();
        Assert.Empty(await ReadResiduesAsync());
        var command = await fixture.CommandAsync(1);
        Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        await context.Set<ProjectNodeBindingRecord>().ExecuteDeleteAsync();
        await context.Set<ProjectObjectRecord>().ExecuteDeleteAsync();
        var residue = Assert.Single(await ReadResiduesAsync());
        Assert.Contains("retained task-plan publication receipts", residue.Description, StringComparison.Ordinal);

        Task<IReadOnlyList<ProjectTransferTargetStateResidue>> ReadResiduesAsync()
            => operations.RunIndependentAsync(profile, (session, token) =>
                operations.InspectTargetAsync(session, participant.FindResiduesAsync, token));
    }

    [Fact]
    public async Task Disclosure_rechecks_target_lifetime_and_dataset_after_awaited_current_read() {
        foreach (var restore in new[] { false, true }) {
            await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
            var command = await fixture.CommandAsync(1);
            var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
            fixture.Authority.BeforeRead = async token => {
                if (restore) {
                    await fixture.Services.GetRequiredService<DatasetActivationCoordinator>().ReactivateAsync(
                        fixture.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile,
                        fixture.Binding.Namespace, Guid.NewGuid(), token);
                } else {
                    await ProjectLifecycleTestCommands.DeleteAsync(fixture.Services, fixture.Project.ProjectId,
                        token, fixture.Project);
                }
            };
            var result = await fixture.LookupAsync(command.Operation);
            Assert.Null(result.Accepted);
            Assert.Equal(restore ? ProjectTaskPlanLookupStatus.DatasetChanged : ProjectTaskPlanLookupStatus.DetailUnavailable, result.Status);
            if (!restore) {
                Assert.Equal(ProjectTaskPlanDetailFailure.TargetUnavailable, result.DetailFailure);
            }
            await using var read = await fixture.ContextAsync();
            var retained = Assert.Single(await read.Set<ProjectTaskPlanReceiptRecord>().AsNoTracking().ToArrayAsync());
            Assert.Equal(ProjectTaskPlanOperationCodec.EncodeAccepted(accepted.Acknowledgement), retained.AcceptedPayload);
        }
    }

    [Fact]
    public async Task Invalid_stored_bytes_versions_and_indexed_facts_withhold_detail_without_running_disclosure() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync(1);
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        Action<ProjectTaskPlanReceiptRecord>[] changes = [
            row => row.AcceptedPayload = [0xff],
            row => row.ResultVersion = 2,
            row => row.DecisionGeneration++,
            row => row.AcceptedPayload = new byte[ProjectTaskPlanReceiptRecord.MaximumPayloadBytes]
        ];
        foreach (var change in changes) {
            await using (var edit = await fixture.ContextAsync()) {
                var row = await edit.Set<ProjectTaskPlanReceiptRecord>().SingleAsync();
                row.AcceptedPayload = ProjectTaskPlanOperationCodec.EncodeAccepted(accepted.Acknowledgement);
                row.ResultVersion = 1;
                row.DecisionGeneration = command.Source.Generation;
                change(row);
                await edit.SaveChangesAsync();
            }
            var observed = await fixture.LookupAsync(command.Operation);
            Assert.Equal(ProjectTaskPlanLookupStatus.DetailUnavailable, observed.Status);
            Assert.Null(observed.Accepted);
            var unresolved = Assert.IsType<ProjectTaskPlanOutcome.Unconfirmed>(
                await fixture.Publisher().PublishAsync(command, fixture.Invocation));
            Assert.Equal(command.Operation, unresolved.Operation);
            Assert.Equal(ProjectTaskPlanRefusal.DetailUnavailable, unresolved.Reason);
            Assert.Equal(0, fixture.Authority.Disclosures);
        }
        FiniteOperationStateRecord originalState;
        FiniteOperationEpochRecord originalEpoch;
        await using (var read = await fixture.ContextAsync()) {
            var receipt = await read.Set<ProjectTaskPlanReceiptRecord>().SingleAsync();
            receipt.AcceptedPayload = ProjectTaskPlanOperationCodec.EncodeAccepted(accepted.Acknowledgement);
            receipt.ResultVersion = 1;
            receipt.DecisionGeneration = command.Source.Generation;
            await read.SaveChangesAsync();
        }
        await using (var read = MetadataContext()) {
            originalState = await read.Set<FiniteOperationStateRecord>().AsNoTracking().SingleAsync(row =>
                row.NamespaceId == command.Operation.Namespace.Value && row.PoolId == fixture.Pool.Pool.Value);
            originalEpoch = await read.Set<FiniteOperationEpochRecord>().AsNoTracking().SingleAsync(row =>
                row.NamespaceId == command.Operation.Namespace.Value && row.PoolId == fixture.Pool.Pool.Value && row.Epoch == command.Operation.Epoch);
        }
        var acquisitions = fixture.Authority.Acquisitions;
        foreach (var failure in Enum.GetValues<RetainedMetadataFailure>()) {
            var disclosures = fixture.Authority.Disclosures;
            try {
                await using (var edit = MetadataContext()) {
                    var states = edit.Set<FiniteOperationStateRecord>().Where(row =>
                        row.NamespaceId == command.Operation.Namespace.Value && row.PoolId == fixture.Pool.Pool.Value);
                    var epochs = edit.Set<FiniteOperationEpochRecord>().Where(row =>
                        row.NamespaceId == command.Operation.Namespace.Value && row.PoolId == fixture.Pool.Pool.Value && row.Epoch == command.Operation.Epoch);
                    var changed = failure switch {
                        RetainedMetadataFailure.UnsupportedProtocol => await states.ExecuteUpdateAsync(update =>
                            update.SetProperty(row => row.RetentionProtocolVersion, 2)),
                        RetainedMetadataFailure.MissingState => await states.ExecuteDeleteAsync(),
                        RetainedMetadataFailure.MissingEpoch => await epochs.ExecuteDeleteAsync(),
                        RetainedMetadataFailure.OpenNoncurrentEpoch => await states.ExecuteUpdateAsync(update =>
                            update.SetProperty(row => row.CurrentEpoch, 2L)),
                        _ => throw new InvalidOperationException()
                    };
                    Assert.Equal(1, changed);
                }
                var observed = await fixture.LookupAsync(command.Operation);
                Assert.Equal(ProjectTaskPlanLookupStatus.Unavailable, observed.Status);
                Assert.Null(observed.Accepted);
                var unresolved = Assert.IsType<ProjectTaskPlanOutcome.Unconfirmed>(
                    await fixture.Publisher().PublishAsync(command, fixture.Invocation));
                Assert.Equal(command.Operation, unresolved.Operation);
                Assert.Equal(ProjectTaskPlanRefusal.CommitUnconfirmed, unresolved.Reason);
                Assert.Equal(acquisitions, fixture.Authority.Acquisitions);
                Assert.Equal(disclosures, fixture.Authority.Disclosures);
                await fixture.VerifyAsync(1, 1, accepted.Acknowledgement);
            } finally {
                await using var restore = MetadataContext();
                restore.Entry(originalState).State = await restore.Set<FiniteOperationStateRecord>().AnyAsync(row =>
                    row.NamespaceId == originalState.NamespaceId && row.PoolId == originalState.PoolId)
                    ? EntityState.Modified : EntityState.Added;
                restore.Entry(originalEpoch).State = await restore.Set<FiniteOperationEpochRecord>().AnyAsync(row =>
                    row.NamespaceId == originalEpoch.NamespaceId && row.PoolId == originalEpoch.PoolId && row.Epoch == originalEpoch.Epoch)
                    ? EntityState.Modified : EntityState.Added;
                await restore.SaveChangesAsync();
            }
            var replay = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
            Assert.True(replay.Replayed);
            Assert.Equal(accepted.Acknowledgement.Tasks, replay.Acknowledgement.Tasks);
            Assert.Equal(acquisitions, fixture.Authority.Acquisitions);
        }

        FiniteOperationDbContext MetadataContext()
            => new(fixture.Services.GetRequiredService<DbContextOptions<FiniteOperationDbContext>>());
    }

    private enum RetainedMetadataFailure { UnsupportedProtocol, MissingState, MissingEpoch, OpenNoncurrentEpoch }

    [Fact]
    public async Task Row_and_byte_bounded_purge_preserves_active_evidence_and_other_owner_pool() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync(1);
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        var projectEvidence = await CreateDefinitionReceiptAsync(fixture);
        var maintenance = fixture.Services.GetRequiredService<FiniteOperationReceiptMaintenance>();
        foreach (var (rows, bytes, firstCount) in new[] { (129, 1, 128), (9, ProjectTaskPlanReceiptRecord.MaximumPayloadBytes, 8) }) {
            var retired = await SeedCleanupOnlyAsync(fixture, accepted.Acknowledgement, rows, bytes);
            Assert.Equal(0, (await maintenance.CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None)).DeletedEpochs);
            var first = await maintenance.PurgeRetiredBatchAsync(fixture.Binding, fixture.Pool, WorkbenchTaskPlanFiniteOperations.Family, CancellationToken.None);
            Assert.Equal(firstCount, first.DeletedReceipts);
            Assert.Equal((long)firstCount * bytes, first.DeletedBytes);
            Assert.Equal(FiniteOperationEligibleWork.Present, first.EligibleWork);
            var last = await maintenance.PurgeRetiredBatchAsync(fixture.Binding, fixture.Pool, WorkbenchTaskPlanFiniteOperations.Family, CancellationToken.None);
            Assert.Equal(rows - firstCount, last.DeletedReceipts);
            Assert.Equal(FiniteOperationEligibleWork.NoneObserved, last.EligibleWork);
            var cleaned = await maintenance.CleanupMetadataAsync(fixture.Binding, fixture.Pool, CancellationToken.None);
            Assert.Equal(1, cleaned.DeletedEpochs);
            Assert.Equal(1, cleaned.DeletedStates);
            await using var read = await fixture.ContextAsync();
            Assert.False(await read.Set<ProjectTaskPlanReceiptRecord>().AnyAsync(row => row.NamespaceId == retired));
        }
        Assert.Equal(accepted.Acknowledgement.Tasks, (await fixture.LookupAsync(command.Operation)).Accepted!.Tasks);
        await using var projects = await fixture.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        Assert.Equal(projectEvidence, (await projects.Set<ProjectDefinitionReceiptRecord>().SingleAsync()).AcceptedPayload);
    }

    [Fact]
    public async Task Locked_retained_row_is_observed_as_remaining_work_and_not_false_empty() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync(1);
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        var retired = await SeedCleanupOnlyAsync(fixture, accepted.Acknowledgement, 1, 16);
        var maintenance = fixture.Services.GetRequiredService<FiniteOperationReceiptMaintenance>();
        await using var holder = await fixture.ContextAsync();
        await using var transaction = await holder.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted);
        await holder.Database.ExecuteSqlInterpolatedAsync($"SELECT \"OperationId\" FROM \"Workbench_TaskPlanReceipts\" WHERE \"NamespaceId\" = {retired} FOR UPDATE");
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var skipped = await maintenance.PurgeRetiredBatchAsync(fixture.Binding, fixture.Pool, WorkbenchTaskPlanFiniteOperations.Family, deadline.Token);
        Assert.Equal(0, skipped.DeletedReceipts);
        Assert.Equal(FiniteOperationEligibleWork.Present, skipped.EligibleWork);
        Assert.Equal(0, (await maintenance.CleanupMetadataAsync(fixture.Binding, fixture.Pool, deadline.Token)).DeletedEpochs);
        await transaction.RollbackAsync(deadline.Token);
        Assert.Equal(1, (await maintenance.PurgeRetiredBatchAsync(fixture.Binding, fixture.Pool,
            WorkbenchTaskPlanFiniteOperations.Family, deadline.Token)).DeletedReceipts);
    }

    [Fact]
    public async Task Coherent_lookup_keeps_copied_evidence_across_concurrent_retirement_and_purge() {
        await using var fixture = await ProjectTaskPlanOperationFixture.CreateAsync();
        var command = await fixture.CommandAsync(1);
        var accepted = Assert.IsType<ProjectTaskPlanOutcome.Accepted>(await fixture.Publisher().PublishAsync(command, fixture.Invocation));
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var cleanup = RetireAfterObservationAsync();
        fixture.Probe.AfterReader = async (sql, token) => {
            if (sql.CommandText.Contains("LEFT JOIN \"Workbench_TaskPlanReceipts\"", StringComparison.Ordinal)) {
                fixture.Probe.AfterReader = null;
                observed.TrySetResult();
                await release.Task.WaitAsync(token);
            }
        };
        ProjectTaskPlanLookup result;
        try {
            result = await fixture.LookupAsync(command.Operation, deadline.Token);
        } finally {
            observed.TrySetResult();
            release.TrySetResult();
            await cleanup;
        }
        Assert.Equal(ProjectTaskPlanLookupStatus.Found, result.Status);
        Assert.Equal(accepted.Acknowledgement.Tasks, result.Accepted!.Tasks);
        Assert.Equal(ProjectTaskPlanLookupStatus.Expired, (await fixture.LookupAsync(command.Operation)).Status);
        await using var verification = await fixture.ContextAsync();
        Assert.Empty(await verification.Set<ProjectTaskPlanReceiptRecord>().ToArrayAsync());

        async Task RetireAfterObservationAsync() {
            try {
                await observed.Task.WaitAsync(deadline.Token);
                await using var owner = await fixture.ContextAsync(deadline.Token);
                await using var transaction = await owner.Database.BeginTransactionAsync(deadline.Token);
                await owner.Database.ExecuteSqlInterpolatedAsync($"""
                    UPDATE "Infrastructure_FiniteOperationEpochs" SET "State" = 2,
                        "OpenedAtUtc" = clock_timestamp() - interval '30 days', "AdmissionClosesAtUtc" = clock_timestamp() - interval '29 days',
                        "ClosedAtUtc" = clock_timestamp() - interval '28 days'
                    WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value} AND "Epoch" = 1;
                    INSERT INTO "Infrastructure_FiniteOperationEpochs"
                        ("NamespaceId", "PoolId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc", "ClosedAtUtc", "MutationDeadlineMilliseconds", "ReplayRetentionSeconds")
                    VALUES ({fixture.Binding.Namespace.Value}, {fixture.Pool.Pool.Value}, 2, 1, clock_timestamp(), clock_timestamp() + interval '1 day', NULL, 30000, 604800);
                    UPDATE "Infrastructure_FiniteOperationState" SET "CurrentEpoch" = 2, "RetiredThroughEpoch" = 1
                    WHERE "NamespaceId" = {fixture.Binding.Namespace.Value} AND "PoolId" = {fixture.Pool.Pool.Value}
                    """, deadline.Token);
                await transaction.CommitAsync(deadline.Token);
                Assert.Equal(1, (await fixture.Services.GetRequiredService<FiniteOperationReceiptMaintenance>()
                    .PurgeRetiredBatchAsync(fixture.Binding, fixture.Pool, WorkbenchTaskPlanFiniteOperations.Family, deadline.Token)).DeletedReceipts);
            } finally {
                release.TrySetResult();
            }
        }
    }

    private static async Task<Guid> SeedCleanupOnlyAsync(ProjectTaskPlanOperationFixture fixture,
        ProjectTaskPlanAccepted accepted, int count, int payloadBytes) {
        var inactive = Guid.NewGuid();
        await using var context = await fixture.ContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync();
        await context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "Infrastructure_FiniteOperationState" ("NamespaceId", "PoolId", "RetentionProtocolVersion", "CurrentEpoch", "RetiredThroughEpoch")
                VALUES ({inactive}, {fixture.Pool.Pool.Value}, 1, 1, 0);
            INSERT INTO "Infrastructure_FiniteOperationEpochs"
                ("NamespaceId", "PoolId", "Epoch", "State", "OpenedAtUtc", "AdmissionClosesAtUtc", "ClosedAtUtc", "MutationDeadlineMilliseconds", "ReplayRetentionSeconds")
                VALUES ({inactive}, {fixture.Pool.Pool.Value}, 1, 2, clock_timestamp() - interval '30 days',
                    clock_timestamp() - interval '29 days', clock_timestamp() - interval '28 days', 30000, 604800)
            """);
        for (var index = 0; index < count; index++) {
            var row = ProjectTaskPlanOperationProtocol.CreateReceipt(accepted, fixture.Caller);
            row.NamespaceId = inactive;
            row.OperationId = Guid.NewGuid();
            row.AcceptedPayload = new byte[payloadBytes];
            context.Add(row);
        }
        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        return inactive;
    }

    private static async Task<byte[]> CreateDefinitionReceiptAsync(ProjectTaskPlanOperationFixture fixture) {
        var invocation = new ProjectDefinitionInvocation(ProjectMutationCallerScope.LocalWorkspace, new DefinitionReadAccess());
        var current = await fixture.Services.GetRequiredService<ProjectDefinitionOperationProtocol>().GetWriteContextAsync(invocation.Caller);
        var command = new CreateProjectDefinitionCommand(new("Other owner", "", "", ProjectStatus.Draft, "", null), [], [], null);
        Assert.IsType<ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted>(await fixture.Services.GetRequiredService<ProjectDefinitionWriter>()
            .CreateAsync(new(new(current.Namespace, current.Epoch, Guid.NewGuid(), current.CallerBinding), ProjectDefinitionRequestVersion.VersionOne, command), invocation));
        await using var read = await fixture.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        return (await read.Set<ProjectDefinitionReceiptRecord>().SingleAsync()).AcceptedPayload;
    }

    private sealed class DefinitionReadAccess : IProjectDefinitionReceiptAccess {
        public Task<bool> CanReadAsync(ProjectWriteAdmission target, CancellationToken cancellationToken) => Task.FromResult(true);
    }
}
