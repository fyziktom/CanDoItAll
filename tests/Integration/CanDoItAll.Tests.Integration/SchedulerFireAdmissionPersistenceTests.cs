using System.Data;
using System.Data.Common;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class SchedulerFireAdmissionPersistenceTests {
    [Fact]
    public async Task Concurrent_fire_prepares_one_identity_and_retry_retains_exact_target_input_and_authorizer() {
        await using var application = await TestApplication.CreateAsync();
        var plan = await SeedAsync(application);
        var request = Fire(plan);
        await using var firstScope = application.Services.CreateAsyncScope();
        await using var secondScope = application.Services.CreateAsyncScope();
        var stores = new[] { firstScope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>(), secondScope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>() };
        var claims = await Task.WhenAll(stores.Select(store => store.AcquireAsync(request)));
        var claim = Assert.Single(claims.OfType<SchedulerFireClaim>());
        Assert.Equal(1, claims.Count(item => item is null));
        await stores[0].CompleteAsync(claim, null, new IOException("Before dispatch"));
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            var edited = await database.Set<SchedulerPlan>().SingleAsync(row => row.Id == plan.Id);
            edited.TargetId = Guid.NewGuid();
            edited.TargetVersionId = Guid.NewGuid();
            edited.InputJson = "{\"changed\":true}";
            edited.StructureAuthorityJson = null;
            await database.SaveChangesAsync();
        }
        var retry = Assert.IsType<SchedulerFireClaim>(await stores[1].AcquireAsync(request with { SchedulerFireId = Guid.NewGuid() }));
        Assert.Equal(claim.Snapshot.ToJson(), retry.Snapshot.ToJson());
        Assert.Equal(claim.PreparedRunId, retry.PreparedRunId);
        Assert.Equal(claim.Snapshot.FireId, retry.Snapshot.FireId);
        Assert.Equal(WorkflowStructureAuthorityChannel.LocalOperator, retry.Snapshot.Authority!.Channel);
        Assert.Equal(WorkflowLaunchActorKind.User, retry.Snapshot.Authority.Principal.Kind);
        await using var readback = await Factory(application).CreateDbContextAsync();
        Assert.Single(await readback.Set<SchedulerFireAdmissionRecord>().Where(row => row.PlanId == plan.Id).ToArrayAsync());
        Assert.Single(await readback.Set<SchedulerPlanRun>().Where(row => row.PlanId == plan.Id).ToArrayAsync());
    }

    [Fact]
    public async Task Superseded_claim_cannot_replace_the_newer_observed_outcome() {
        await using var application = await TestApplication.CreateAsync();
        var plan = await SeedAsync(application);
        await using var scope = application.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>();
        var first = Assert.IsType<SchedulerFireClaim>(await store.AcquireAsync(Fire(plan)));
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            var row = await database.Set<SchedulerFireAdmissionRecord>().SingleAsync(item => item.Id == first.Snapshot.PlanRunId);
            row.LeaseExpiresAtUtc = scope.ServiceProvider.GetRequiredService<IClock>().GetUtcNow().AddMinutes(-10);
            await database.SaveChangesAsync();
        }
        var second = Assert.IsType<SchedulerFireClaim>(await store.AcquireAsync(Fire(plan)));
        Assert.True(second.Generation > first.Generation);
        Assert.Equal(first.PreparedRunId, second.PreparedRunId);
        await store.CompleteAsync(second, Completed(second), null);
        await store.CompleteAsync(first, null, new IOException("Stale completion"));
        await using var readback = await Factory(application).CreateDbContextAsync();
        var saved = await readback.Set<SchedulerFireAdmissionRecord>().SingleAsync(row => row.Id == first.Snapshot.PlanRunId);
        Assert.Equal(SchedulerFireAdmissionState.Observed, saved.State);
        Assert.Equal(second.PreparedRunId, saved.AcceptedWorkflowRunId);
        Assert.Equal(string.Empty, saved.LastError);
    }

    [Fact]
    public async Task Plan_deletion_keeps_admission_and_recreated_id_cannot_relaunch_the_same_fire() {
        await using var application = await TestApplication.CreateAsync();
        var plan = await SeedAsync(application);
        await using var scope = application.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>();
        var claim = Assert.IsType<SchedulerFireClaim>(await store.AcquireAsync(Fire(plan)));
        await store.CompleteAsync(claim, Completed(claim), null);
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            database.Remove(await database.Set<SchedulerPlan>().SingleAsync(row => row.Id == plan.Id));
            await database.SaveChangesAsync();
            Assert.False(await database.Set<SchedulerPlanRun>().AnyAsync(row => row.PlanId == plan.Id));
            Assert.True(await database.Set<SchedulerFireAdmissionRecord>().AnyAsync(row => row.Id == claim.Snapshot.PlanRunId));
            database.Add(NewPlan(plan.Id));
            await database.SaveChangesAsync();
        }
        Assert.Null(await store.AcquireAsync(Fire(plan)));
    }

    [Fact]
    public async Task Lost_commit_ack_preserves_original_exception_and_observed_receipt_after_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("scheduler-admission-lost-ack");
        var harness = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = environment.CreatePostgreSqlProfile("owner") };
        Guid admissionId;
        Guid preparedRunId;
        SchedulerPlan plan;
        var failure = new ArgumentException("Commit acknowledgement lost");
        await using (var application = await TestApplication.CreateAsync(harness)) {
            plan = await SeedAsync(application);
            await using var scope = application.Services.CreateAsyncScope();
            var fault = new CommitFault(failure);
            var options = new DbContextOptionsBuilder<SchedulerPlannerDbContext>(scope.ServiceProvider.GetRequiredService<DbContextOptions<SchedulerPlannerDbContext>>())
                .AddInterceptors(fault).Options;
            var store = new SchedulerFireAdmissionStore(new ContextFactory(options), scope.ServiceProvider.GetRequiredService<IWorkflowCatalogService>(), scope.ServiceProvider.GetRequiredService<IWorkflowRuntimeManager>(),
                scope.ServiceProvider.GetRequiredService<IClock>());
            var claim = Assert.IsType<SchedulerFireClaim>(await store.AcquireAsync(Fire(plan)));
            admissionId = claim.Snapshot.PlanRunId;
            preparedRunId = claim.PreparedRunId;
            fault.Armed = true;
            Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => store.CompleteAsync(claim, Completed(claim), null)));
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        Assert.Null(await restartedScope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>().AcquireAsync(Fire(plan)));
        await using var readback = await Factory(restarted).CreateDbContextAsync();
        var row = await readback.Set<SchedulerFireAdmissionRecord>().SingleAsync(item => item.Id == admissionId);
        Assert.Equal(preparedRunId, row.AcceptedWorkflowRunId);
        Assert.Equal(SchedulerFireAdmissionState.Observed, row.State);
        Assert.NotNull(row.OutcomeJson);
        Assert.Contains("Confirmed completion", row.OutcomeJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Post_admission_observer_failure_is_persisted_before_original_exception_and_retry_uses_same_run() {
        await using var application = await TestApplication.CreateAsync();
        var plan = await SeedAsync(application);
        await using var scope = application.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>();
        var failure = new ArgumentException("Lost Workflow observation");
        var launcher = new RecordingLauncher(failure);
        var dispatcher = new SchedulerPlannerRunDispatcher(store, launcher, NullLogger<SchedulerPlannerRunDispatcher>.Instance);
        Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => dispatcher.DispatchAsync(Fire(plan))));
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            var history = await database.Set<SchedulerPlanRun>().SingleAsync(row => row.PlanId == plan.Id);
            Assert.Equal(SchedulerPlanRunDispatchStatus.ObservationPending, history.Status);
            Assert.Equal(launcher.RunIds[0], history.TargetRunId);
            Assert.Equal(failure.Message, history.ErrorMessage);
        }
        launcher.Failure = null;
        await dispatcher.DispatchAsync(Fire(plan));
        Assert.Equal(2, launcher.RunIds.Count);
        Assert.Equal(launcher.RunIds[0], launcher.RunIds[1]);
        await using var readback = await Factory(application).CreateDbContextAsync();
        Assert.Equal(SchedulerFireAdmissionState.Observed, (await readback.Set<SchedulerFireAdmissionRecord>().SingleAsync(row => row.PlanId == plan.Id)).State);
    }

    [Fact]
    public async Task Current_disabled_or_replaced_authority_denies_effects_without_changing_saved_ceiling() {
        await using var application = await TestApplication.CreateAsync();
        var plan = await SeedAsync(application);
        await using var scope = application.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>();
        var claim = Assert.IsType<SchedulerFireClaim>(await store.AcquireAsync(Fire(plan)));
        var binding = claim.Snapshot.ToContext(claim.PreparedRunId, true).StructureAuthority!.SchedulerAuthority!;
        var policy = scope.ServiceProvider.GetRequiredService<IWorkflowScheduledAuthorityPolicy>();
        await policy.RequireCurrentAsync(binding);
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            var saved = await database.Set<SchedulerPlan>().SingleAsync(row => row.Id == plan.Id);
            saved.IsEnabled = false;
            await database.SaveChangesAsync();
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => policy.RequireCurrentAsync(binding));
            saved.IsEnabled = true;
            saved.StructureAuthorityJson = null;
            await database.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => policy.RequireCurrentAsync(binding));
        Assert.Equal(WorkflowStructureAuthorityChannel.LocalOperator, claim.Snapshot.Authority!.Channel);
        Assert.True(claim.Snapshot.Authority.CanCreateTasks);
    }

    [Fact]
    public async Task Mutation_policy_uses_owner_transaction_while_normal_reads_remain_independent() {
        await using var application = await TestApplication.CreateAsync();
        var plan = await SeedAsync(application);
        await using var scope = application.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>();
        var claim = Assert.IsType<SchedulerFireClaim>(await store.AcquireAsync(Fire(plan)));
        var binding = claim.Snapshot.ToContext(claim.PreparedRunId, true).StructureAuthority!.SchedulerAuthority!;
        var policy = scope.ServiceProvider.GetRequiredService<IWorkflowScheduledAuthorityPolicy>();
        await using var owner = await Factory(application).CreateDbContextAsync();
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        using var coordination = scope.ServiceProvider.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(owner);
        await policy.RequireCurrentForMutationAsync(binding);
        var staged = await owner.Set<SchedulerPlan>().SingleAsync(item => item.Id == plan.Id);
        staged.IsEnabled = false;
        await owner.SaveChangesAsync();
        await policy.RequireCurrentAsync(binding);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => policy.RequireCurrentForMutationAsync(binding));
        await transaction.RollbackAsync();
        coordination.Dispose();
        await policy.RequireCurrentAsync(binding);
        await using (var independent = await Factory(application).CreateDbContextAsync()) {
            var row = await independent.Set<SchedulerPlan>().SingleAsync(item => item.Id == plan.Id);
            row.IsEnabled = false;
            await independent.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => policy.RequireCurrentAsync(binding));
    }

    [Fact]
    public async Task Legacy_attempted_fire_is_observable_and_cannot_adopt_todays_input_on_retry() {
        await using var application = await TestApplication.CreateAsync();
        var plan = await SeedAsync(application);
        var fire = Fire(plan);
        var history = new SchedulerPlanRun {
            PlanId = plan.Id, DedupeKey = $"scheduler-planner:{plan.Id:N}:{fire.FiredAtUtc.UtcTicks}",
            SchedulerFireId = fire.SchedulerFireId, FiredAtUtc = fire.FiredAtUtc,
            Status = SchedulerPlanRunDispatchStatus.Failed, AttemptCount = 1, Summary = "Retained legacy failure"
        };
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            database.Add(history);
            await database.SaveChangesAsync();
        }
        await using var scope = application.Services.CreateAsyncScope();
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            scope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>().AcquireAsync(fire));
        Assert.Contains("original intent", exception.Message, StringComparison.Ordinal);
        await using var readback = await Factory(application).CreateDbContextAsync();
        Assert.Equal(history.Summary, (await readback.Set<SchedulerPlanRun>().SingleAsync(row => row.Id == history.Id)).Summary);
        Assert.False(await readback.Set<SchedulerFireAdmissionRecord>().AnyAsync(row => row.PlanId == plan.Id));
    }

    [Fact]
    public async Task Legacy_known_run_reconstructs_observation_from_saved_origin_without_authority_or_current_target_adoption() {
        await using var application = await TestApplication.CreateAsync();
        var plan = await SeedAsync(application);
        var fire = Fire(plan);
        var history = new SchedulerPlanRun {
            PlanId = plan.Id, DedupeKey = $"scheduler-planner:{plan.Id:N}:{fire.FiredAtUtc.UtcTicks}",
            SchedulerFireId = fire.SchedulerFireId, CorrelationId = fire.CorrelationId, FiredAtUtc = fire.FiredAtUtc,
            Status = SchedulerPlanRunDispatchStatus.Failed, TargetRunId = Guid.NewGuid(), TargetRunKind = "Workflow", AttemptCount = 1
        };
        var originalWorkflowId = WorkflowId.New();
        var originalVersionId = WorkflowVersionId.New();
        await using var scope = application.Services.CreateAsyncScope();
        var run = new WorkflowRunSnapshot(new(history.TargetRunId.Value), originalWorkflowId, originalVersionId, WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess, "legacy-backend", "Original result", fire.FiredAtUtc, fire.FiredAtUtc) {
            Origin = new WorkflowLaunchOrigin.SchedulerPlanRun(plan.Id, history.Id, new(fire.SchedulerFireId), fire.FiredAtUtc, new(fire.CorrelationId!.Value))
        };
        var runs = scope.ServiceProvider.GetRequiredService<IWorkflowRunStore>();
        await Assert.ThrowsAsync<WorkflowScheduledSourceAuthorityException>(() => runs.SaveRunAsync(run));
        Assert.Null(await runs.GetRunAsync(run.RunId));
        var legacyRow = WorkflowRunRecordEntity.FromSnapshot(run);
        // Seed pre-authority history without admitting a new scheduled execution.
        await using (var historical = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync()) {
            historical.Add(legacyRow);
            await historical.SaveChangesAsync();
        }
        await using (var database = await Factory(application).CreateDbContextAsync()) {
            database.Add(history);
            var current = await database.Set<SchedulerPlan>().SingleAsync(row => row.Id == plan.Id);
            current.IsEnabled = false;
            await database.SaveChangesAsync();
        }
        var store = scope.ServiceProvider.GetRequiredService<SchedulerFireAdmissionStore>();
        var claim = Assert.IsType<SchedulerFireClaim>(await store.AcquireAsync(fire));
        Assert.True(claim.Snapshot.LegacyObservationOnly);
        Assert.False(claim.MayLaunch);
        Assert.Null(claim.Snapshot.Authority);
        Assert.Equal(run.RunId.Value, claim.PreparedRunId);
        Assert.Equal(originalWorkflowId.Value, claim.Snapshot.TargetId);
        Assert.Equal(originalVersionId.Value, claim.Snapshot.TargetVersionId);
        var result = await scope.ServiceProvider.GetRequiredService<ISchedulerTargetLauncher>()
            .LaunchAsync(claim.Snapshot.ToPlan(), claim.Snapshot.ToContext(claim.PreparedRunId, claim.MayLaunch));
        Assert.Equal(run.RunId.Value, result.TargetRunId);
        Assert.Equal(WorkflowRunState.Completed, result.WorkflowState);
        await store.CompleteAsync(claim, result, null);
        await using var readback = await Factory(application).CreateDbContextAsync();
        Assert.Equal(SchedulerFireAdmissionState.Observed, (await readback.Set<SchedulerFireAdmissionRecord>().SingleAsync(row => row.Id == history.Id)).State);
        await using var workflowReadback = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkflowDbContext>>().CreateDbContextAsync();
        var retained = await workflowReadback.Set<WorkflowRunRecordEntity>().AsNoTracking().SingleAsync(row => row.RunId == run.RunId.Value);
        Assert.Equal(legacyRow.OriginJson, retained.OriginJson);
        Assert.Null(retained.ToSnapshot().Origin!.StructureAuthority);
        Assert.Equal(originalWorkflowId.Value, retained.WorkflowId);
        Assert.Equal(originalVersionId.Value, retained.VersionId);
        Assert.Equal(WorkflowRunState.Completed, retained.State);
    }

    private static IDbContextFactory<SchedulerPlannerDbContext> Factory(TestApplication application) =>
        application.Services.GetRequiredService<IDbContextFactory<SchedulerPlannerDbContext>>();

    private static async Task<SchedulerPlan> SeedAsync(TestApplication application) {
        var plan = NewPlan(Guid.NewGuid());
        var profileId = application.Services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile.Profile.Id;
        plan.StructureAuthorityJson = SchedulerFireSnapshot.SerializeAuthority(new(WorkflowStructureAuthorityChannel.LocalOperator,
            new(WorkflowLaunchActorKind.User, "local-operator"), profileId, Guid.Empty, true, true, null, "fixture-policy") { AllProjects = true });
        await using var database = await Factory(application).CreateDbContextAsync();
        database.Add(plan);
        await database.SaveChangesAsync();
        return plan;
    }

    private static SchedulerPlan NewPlan(Guid id) => new() {
        Id = id, Name = "Prepared fire", TargetKind = SchedulerPlanTargetKind.Workflow, TargetId = Guid.NewGuid(), TargetVersionId = Guid.NewGuid(),
        TargetNameSnapshot = "Original Workflow", InputJson = "{\"original\":true}", SchedulerTriggerId = Guid.NewGuid(),
        SchedulerTriggerKey = $"fixture-{Guid.NewGuid():N}", CronExpression = "0 0/30 * * * ?", CronDescription = "Every thirty minutes"
    };

    private static SchedulerPlanFireRequest Fire(SchedulerPlan plan) => new(plan.Id, plan.Id, plan.Id,
        new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero), null);
    private static SchedulerTargetLaunchResult Completed(SchedulerFireClaim claim) => new(SchedulerPlanTargetKind.Workflow,
        claim.PreparedRunId, "Completed", "Confirmed completion") { WorkflowState = WorkflowRunState.Completed };

    private sealed class ContextFactory(DbContextOptions<SchedulerPlannerDbContext> options) : IDbContextFactory<SchedulerPlannerDbContext> {
        public SchedulerPlannerDbContext CreateDbContext() => new(options);
        public Task<SchedulerPlannerDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class CommitFault(Exception failure) : DbTransactionInterceptor {
        public bool Armed { get; set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (Armed) {
                Armed = false;
                throw failure;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingLauncher(Exception? failure) : ISchedulerTargetLauncher {
        public Exception? Failure { get; set; } = failure;
        public List<Guid> RunIds { get; } = [];
        public Task<SchedulerTargetLaunchResult> LaunchAsync(SchedulerPlan plan, SchedulerTargetLaunchContext context, CancellationToken cancellationToken = default) {
            var runId = context.PreparedRunId!.Value.Value;
            RunIds.Add(runId);
            return Task.FromResult(new SchedulerTargetLaunchResult(plan.TargetKind, runId, "Completed", "Confirmed completion",
                Failure is null ? SchedulerPlanRunDispatchStatus.Dispatched : SchedulerPlanRunDispatchStatus.ObservationPending) {
                WorkflowState = WorkflowRunState.Completed, RequiresObservation = Failure is not null, ObservationException = Failure
            });
        }
    }
}
