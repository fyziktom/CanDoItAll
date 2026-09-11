using System.Data.Common;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

public sealed class ProcessPreparedLaunchPersistenceTests {
    [Fact]
    public async Task Concurrent_same_intent_preparations_use_one_exact_review_run_and_admission_sequence() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var authority = ProcessPreparedLaunchFixture.Local(Profile(services).Profile.Id);
        var intent = new ProcessLaunchIntentId(Guid.NewGuid());
        var first = ProcessPreparedLaunchFixture.Create(authority, intent);
        var second = ProcessPreparedLaunchFixture.Create(authority, intent);
        Assert.NotEqual(first.InitialCommit.Mutation.State.RunId, second.InitialCommit.Mutation.State.RunId);
        Assert.Equal(first.RequestFingerprint, second.RequestFingerprint);
        var attempts = await Task.WhenAll(Store(services).PrepareAsync(first), Store(services).PrepareAsync(second));
        Assert.Equal(attempts[0].Preparation.AdmissionId, attempts[1].Preparation.AdmissionId);
        Assert.Equal(attempts[0].PreparationFingerprint, attempts[1].PreparationFingerprint);
        Assert.Equal(attempts[0].Preparation.Review.PlanId, attempts[1].Preparation.Review.PlanId);
        Assert.Equal(attempts[0].AdmissionSequence, attempts[1].AdmissionSequence);
        Assert.True(attempts[0].AdmissionSequence > 0);
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
        Assert.Equal(1, await context.PreparedLaunches.CountAsync(item => item.CallerIntentId == intent.Value));
        Assert.Empty(await context.RuntimeStates.ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reusing_intent_with_changed_content_or_target_conflicts_without_replacing_review(bool changeTarget) {
        await using var app = await TestApplication.CreateAsync(Harness());
        var services = app.Services;
        var first = ProcessPreparedLaunchFixture.Create(ProcessPreparedLaunchFixture.Local(Profile(services).Profile.Id), new(Guid.NewGuid()));
        var store = Store(services);
        var saved = await store.PrepareAsync(first);
        var changedRequest = changeTarget ? first.Request with { ProjectNodeId = "another-source" } :
            first.Request with { Variables = new Dictionary<string, string> { ["Topic"] = "changed content" } };
        var changed = first with { Request = changedRequest, RequestFingerprint = ProcessLaunchIntentFingerprint.Compute(changedRequest) };
        await Assert.ThrowsAsync<ProcessLaunchIntentConflictException>(() => store.PrepareAsync(changed));
        var retained = Assert.IsType<ProcessPreparedLaunchSnapshot>(await store.GetAsync(saved.Preparation.AdmissionId));
        Assert.Equal(saved.PreparationFingerprint, retained.PreparationFingerprint);
        Assert.Equal(saved.Preparation.Review.PlanId, retained.Preparation.Review.PlanId);
    }

    [Fact]
    public async Task Failure_after_actual_initial_flush_rolls_back_acceptance_and_all_initial_runtime_records() {
        await using var app = await TestApplication.CreateAsync(Harness());
        var services = app.Services;
        var coordinator = Coordinator(services);
        var saved = await Store(services).PrepareAsync(ProcessPreparedLaunchFixture.Create());
        var request = ProcessPreparedLaunchFixture.Commit(saved);
        var failure = new ArgumentException("Injected failure after initial Process flush.");
        var fault = new AfterFlushFault(failure);
        await using (var context = new ProcessPersistenceDbContext(ProcessOptions(services, fault))) {
            Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => new EfProcessRuntimeUnitOfWork(context).CommitAsync(request)));
            Assert.True(fault.Flushed);
        }
        var retained = Assert.IsType<ProcessPreparedLaunchSnapshot>(await Store(services).GetAsync(saved.Preparation.AdmissionId));
        Assert.Null(retained.AcceptedAtUtc);
        Assert.Equal(ProcessLaunchContinuationState.Prepared, retained.State);
        await using var retryContext = new ProcessPersistenceDbContext(ProcessOptions(services));
        await AssertInitialRowsAsync(retryContext, request, 0);
        Assert.True((await new EfProcessRuntimeUnitOfWork(retryContext, coordinatedTransaction: coordinator).CommitAsync(request)).Succeeded);
        await AssertInitialRowsAsync(retryContext, request, 1);
        Assert.Equal(saved.Preparation.AdmissionId, (await new EfProcessRuntimeUnitOfWork(retryContext).LoadAsync(request.Mutation.State.RunId))!.LaunchAdmissionId);
    }

    [Fact]
    public async Task Application_preserves_exact_commit_ack_exception_and_accepted_identity_through_full_host_restart() {
        await using var environment = CanDoItAllTestEnvironment.Create("process-prepared-ack");
        var profile = environment.CreatePostgreSqlProfile("prepared-ack");
        var harness = Harness(environment, profile);
        ProcessPreparedLaunchSnapshot saved;
        var failure = new ArgumentException("Injected accepted Process acknowledgement loss.");
        await using (var app = await TestApplication.CreateAsync(harness)) {
            var services = app.Services;
            var authority = ProcessPreparedLaunchFixture.Local(Profile(services).Profile.Id);
            var store = Store(services);
            saved = await store.PrepareAsync(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())));
            await using var context = new ProcessPersistenceDbContext(ProcessOptions(services, new AfterCommitFault(failure)));
            var policy = new AuthorityPolicy();
            var unitOfWork = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: Coordinator(services), launchAuthorityPolicy: policy);
            var result = await Service(unitOfWork, store, policy).LaunchAsync(Request(saved));
            Assert.Equal(saved.Preparation.InitialCommit.Mutation.State.RunId, result.RunId);
            Assert.Equal(ProcessLaunchContinuationState.Accepted, result.Observation!.ContinuationState);
            Assert.Same(failure, result.Observation.ObservationException);
            Assert.False(policy.Held);
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        var retained = Assert.IsType<ProcessPreparedLaunchSnapshot>(await Store(restarted.Services).GetAsync(saved.Preparation.AdmissionId));
        Assert.NotNull(retained.AcceptedAtUtc);
        Assert.Equal(saved.PreparationFingerprint, retained.PreparationFingerprint);
        await using var current = new ProcessPersistenceDbContext(ProcessOptions(restarted.Services));
        var ordinary = new EfProcessRuntimeUnitOfWork(current);
        var replay = await ordinary.CommitAsync(ProcessPreparedLaunchFixture.Commit(retained));
        Assert.Equal(saved.Preparation.InitialCommit.Mutation.Outcome, replay.Outcome);
        Assert.Equal(saved.Preparation.InitialCommit.Mutation.State.RunId, replay.State.RunId);
        await AssertInitialRowsAsync(current, saved.Preparation.InitialCommit, 1);
        Assert.True((await ordinary.CommitAsync(ProcessProjectAdmissionFixture.Cancel(replay.State))).Succeeded);
        Assert.Equal(saved.Preparation.AdmissionId, (await ordinary.LoadAsync(replay.State.RunId))!.LaunchAdmissionId);
    }

    [Fact]
    public async Task Lost_continuation_ack_preserves_ArgumentException_object_and_replay_does_not_compile_another_plan() {
        await using var app = await TestApplication.CreateAsync(Harness());
        var services = app.Services;
        var authority = ProcessPreparedLaunchFixture.Local(Profile(services).Profile.Id);
        var store = Store(services);
        var saved = await store.PrepareAsync(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid())));
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
        var policy = new AuthorityPolicy();
        var unitOfWork = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: Coordinator(services), launchAuthorityPolicy: policy);
        Assert.True((await unitOfWork.CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).Succeeded);
        var failure = new ArgumentException("Injected artifact initialization acknowledgement loss.");
        var artifacts = new ThrowingArtifacts(failure);
        var service = Service(unitOfWork, store, policy, artifacts);
        var result = await service.LaunchAsync(Request(saved));
        Assert.Equal(saved.Preparation.InitialCommit.Mutation.State.RunId, result.RunId);
        Assert.Same(failure, result.Observation!.ObservationException);
        Assert.Equal(1, artifacts.Calls);
        var replay = await service.LaunchAsync(Request(saved));
        Assert.Equal(result.RunId, replay.RunId);
        Assert.Equal(result.LaunchPlanId, replay.LaunchPlanId);
        Assert.Equal(1, artifacts.Calls);
        await AssertInitialRowsAsync(context, saved.Preparation.InitialCommit, 1);
    }

    [Fact]
    public async Task Current_source_lease_is_acquired_before_SQL_and_remains_held_until_the_actual_commit() {
        await using var app = await TestApplication.CreateAsync(Harness());
        var services = app.Services;
        var coordinator = Coordinator(services);
        var saved = await Store(services).PrepareAsync(ProcessPreparedLaunchFixture.Create(
            ProcessPreparedLaunchFixture.Local(Profile(services).Profile.Id), new(Guid.NewGuid())));
        var policy = new AuthorityPolicy();
        var commit = new CommitLeaseProbe(policy);
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services, commit));
        policy.Acquiring = () => Assert.Null(context.Database.CurrentTransaction);
        policy.Validating = async (_, ct) => {
            Assert.NotNull(context.Database.CurrentTransaction);
            await using var participant = await coordinator.CreateEnlistedAsync(ProcessOptions(services), options => new ProcessPersistenceDbContext(options), ct);
            Assert.Same(context.Database.GetDbConnection(), participant.Database.GetDbConnection());
            Assert.Same(context.Database.CurrentTransaction!.GetDbTransaction(), participant.Database.CurrentTransaction!.GetDbTransaction());
            Assert.True(await participant.PreparedLaunches.AnyAsync(item => item.Id == saved.Preparation.AdmissionId.Value, ct));
        };
        var unitOfWork = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: coordinator, launchAuthorityPolicy: policy);
        Assert.True((await unitOfWork.CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).Succeeded);
        Assert.True(commit.Observed);
        Assert.True(commit.CommittedObserved);
        Assert.Equal(1, policy.Validations);
        Assert.False(policy.Held);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Current_authority_denial_before_or_inside_SQL_leaves_preparation_unaccepted(bool duringTransaction) {
        await using var app = await TestApplication.CreateAsync(Harness());
        var services = app.Services;
        var saved = await Store(services).PrepareAsync(ProcessPreparedLaunchFixture.Create(
            ProcessPreparedLaunchFixture.Local(Profile(services).Profile.Id), new(Guid.NewGuid())));
        var failure = new ProcessLaunchAuthorityRejectedException("The source grant was revoked.");
        var policy = new AuthorityPolicy();
        if (duringTransaction) {
            policy.Validating = (_, _) => throw failure;
        } else {
            policy.Acquiring = () => throw failure;
        }
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
        var unitOfWork = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: Coordinator(services), launchAuthorityPolicy: policy);
        Assert.Same(failure, await Assert.ThrowsAsync<ProcessLaunchAuthorityRejectedException>(() => unitOfWork.CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))));
        Assert.False(policy.Held);
        await AssertInitialRowsAsync(context, saved.Preparation.InitialCommit, 0);
        Assert.Null((await Store(services).GetAsync(saved.Preparation.AdmissionId))!.AcceptedAtUtc);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Later_runtime_commands_cannot_remove_or_replace_retained_launch_admission(bool replace) {
        await using var app = await TestApplication.CreateAsync(Harness());
        var services = app.Services;
        var saved = await Store(services).PrepareAsync(ProcessPreparedLaunchFixture.Create());
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
        var unitOfWork = new EfProcessRuntimeUnitOfWork(context);
        var accepted = await unitOfWork.CommitAsync(ProcessPreparedLaunchFixture.Commit(saved));
        var cancel = ProcessProjectAdmissionFixture.Cancel(accepted.State);
        var changed = replace ? new ProcessLaunchAdmissionId(Guid.NewGuid()) : (ProcessLaunchAdmissionId?)null;
        cancel = cancel with {
            OriginalState = cancel.OriginalState with { LaunchAdmissionId = changed },
            Mutation = cancel.Mutation with { State = cancel.Mutation.State with { LaunchAdmissionId = changed } }
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.CommitAsync(cancel));
        Assert.Equal(saved.Preparation.AdmissionId, (await unitOfWork.LoadAsync(accepted.State.RunId))!.LaunchAdmissionId);
        await AssertInitialRowsAsync(context, saved.Preparation.InitialCommit, 1);
    }

    [Fact]
    public async Task Changed_native_source_binding_is_rechecked_on_the_actual_Process_transaction_before_acceptance() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var project = await CreateProjectAsync(services);
        var workbench = services.GetRequiredService<ProjectWorkbenchService>();
        var root = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(project.ProjectId);
        var source = await workbench.CreateObjectAsync(project.ProjectId, new(ProjectObjectType.Note, "Reviewed source", "", "", root));
        var target = await Targets(services, Coordinator(services)).CaptureAsync(project.ProjectId, source.Id);
        var authority = ProcessPreparedLaunchFixture.Local(project.DatabaseProfileId, project);
        var saved = await Store(services).PrepareAsync(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()), target));
        var container = await workbench.CreateObjectAsync(project.ProjectId, new(ProjectObjectType.ProjectBlock, "New parent", "", "", root));
        Assert.NotNull(await services.GetRequiredService<ProjectWorkbenchRelationService>().ReparentObjectAsync(project.ProjectId, source.Id, container.Id));
        var coordinator = Coordinator(services);
        var policy = new AuthorityPolicy { Validating = (binding, ct) => Targets(services, coordinator).RequireForMutationAsync(binding!, ct) };
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
        var unitOfWork = new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: coordinator,
            projectAdmissionPolicy: ProjectPolicy(services, coordinator), launchAuthorityPolicy: policy);
        await Assert.ThrowsAsync<ProcessLaunchIntentConflictException>(() => unitOfWork.CommitAsync(ProcessPreparedLaunchFixture.Commit(saved)));
        await AssertInitialRowsAsync(context, saved.Preparation.InitialCommit, 0);
        Assert.False(policy.Held);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_link_and_Process_receipt_share_both_flushes_and_recover_exact_lost_ack(bool afterCommit) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var saved = await CreateAcceptedProjectLaunchAsync(services);
        var failure = new ArgumentException("Injected native Process link acknowledgement failure.");
        var coordinator = Coordinator(services);
        var commands = new LinkCommandProbe();
        var flush = new AfterFlushFault(failure);
        var ownerOptions = afterCommit ? WorkbenchOptions(services, commands, new AfterCommitFault(failure)) : WorkbenchOptions(services, commands);
        var receiptOptions = afterCommit ? ProcessOptions(services, commands) : ProcessOptions(services, commands, flush);
        var receipts = new EfProcessPreparedLaunchStore(new ProcessFactory(ProcessOptions(services)), receiptOptions, coordinatedTransaction: coordinator);
        var delivery = Delivery(services, coordinator, ownerOptions, receipts);
        Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => delivery.DeliverAsync(saved.Preparation.AdmissionId)));
        Assert.True(commands.NativeFlushed);
        Assert.True(commands.ReceiptFlushed);
        Assert.Same(commands.Connection, commands.ReceiptConnection);
        Assert.Same(commands.Transaction, commands.ReceiptTransaction);
        var observed = Assert.IsType<ProcessPreparedLaunchSnapshot>(await Store(services).GetAsync(saved.Preparation.AdmissionId));
        Assert.Equal(afterCommit ? ProcessLaunchLinkDeliveryState.Delivered : ProcessLaunchLinkDeliveryState.Pending, observed.LinkDeliveryState);
        if (!afterCommit) {
            Assert.True(flush.Flushed);
        }
        var recovered = await Delivery(services, coordinator).DeliverAsync(saved.Preparation.AdmissionId);
        Assert.Equal(ProcessLaunchLinkDeliveryState.Delivered, recovered.State);
        if (afterCommit) {
            Assert.Equal(observed.DeliveredLinkId, recovered.LinkId);
        }
        await using var context = new WorkbenchDbContext(WorkbenchOptions(services));
        var runNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(saved.Preparation.InitialCommit.Mutation.State.RunId.Value);
        Assert.Equal(1, await context.Set<ProjectObjectLinkRecord>().CountAsync(link => link.ProjectId == saved.Preparation.LinkTarget!.ProjectId && link.TargetNodeKey == runNodeKey && !link.IsSystemManaged));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Restart_then_human_unlink_or_project_deletion_keeps_original_receipt_and_never_recreates_link(bool deleteProject) {
        await using var environment = CanDoItAllTestEnvironment.Create("process-link-retention");
        var profile = environment.CreatePostgreSqlProfile("link-retention");
        var harness = Harness(environment, profile);
        ProcessPreparedLaunchSnapshot saved;
        Guid? originalLink;
        await using (var app = await TestApplication.CreateAsync(harness)) {
            await using var scope = app.Services.CreateAsyncScope();
            var services = scope.ServiceProvider;
            saved = await CreateAcceptedProjectLaunchAsync(services);
            var receipt = await Delivery(services, Coordinator(services)).DeliverAsync(saved.Preparation.AdmissionId);
            originalLink = receipt.LinkId;
        }
        await using var restarted = await TestApplication.CreateAsync(harness);
        await using var currentScope = restarted.Services.CreateAsyncScope();
        var current = currentScope.ServiceProvider;
        var target = saved.Preparation.LinkTarget!;
        var targetNodeKey = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(saved.Preparation.InitialCommit.Mutation.State.RunId.Value);
        if (deleteProject) {
            await current.GetRequiredService<ProjectsService>().DeleteAsync(target.ProjectId);
        } else {
            Assert.True(await current.GetRequiredService<ProjectWorkbenchRelationService>().UnlinkObjectsAsync(
                target.ProjectId, target.SourceNodeKey, targetNodeKey, ProjectObjectLinkKind.Uses));
        }
        var service = Delivery(current, Coordinator(current));
        var status = await service.GetStatusAsync(saved.Preparation.AdmissionId);
        Assert.Equal(ProcessLaunchLinkDeliveryState.Removed, status.State);
        Assert.Equal(ProcessLaunchLinkDeliveryState.Delivered, (await Store(current).GetAsync(saved.Preparation.AdmissionId))!.LinkDeliveryState);
        var replay = await service.DeliverAsync(saved.Preparation.AdmissionId);
        Assert.Equal(ProcessLaunchLinkDeliveryState.Removed, replay.State);
        Assert.Equal(originalLink, replay.LinkId);
        Assert.Equal(replay, await service.DeliverAsync(saved.Preparation.AdmissionId));
        var retained = (await Store(current).GetAsync(saved.Preparation.AdmissionId))!;
        Assert.Equal(saved.PreparationFingerprint, retained.PreparationFingerprint);
        Assert.Equal(ProcessLaunchLinkDeliveryState.Removed, retained.LinkDeliveryState);
        await using var database = new WorkbenchDbContext(WorkbenchOptions(current));
        Assert.False(await database.Set<ProjectObjectLinkRecord>().AnyAsync(link => link.Id == originalLink));
    }

    [Fact]
    public async Task Project_retirement_before_first_delivery_records_a_durable_conflict_without_creating_a_link() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var saved = await CreateAcceptedProjectLaunchAsync(services);
        await services.GetRequiredService<ProjectsService>().DeleteAsync(saved.Preparation.LinkTarget!.ProjectId);
        var delivery = Delivery(services, Coordinator(services));
        var result = await delivery.DeliverAsync(saved.Preparation.AdmissionId);
        Assert.Equal(ProcessLaunchLinkDeliveryState.Conflict, result.State);
        Assert.Equal(ProcessLaunchLinkConflictReason.ProjectRetired, result.ConflictReason);
        Assert.IsType<ProjectWriteAdmissionRejectedException>(result.ObservationException);
        Assert.Null(result.LinkId);
        var replay = await delivery.DeliverAsync(saved.Preparation.AdmissionId);
        Assert.Equal(result.State, replay.State);
        Assert.Equal(result.ConflictReason, replay.ConflictReason);
        var retained = (await Store(services).GetAsync(saved.Preparation.AdmissionId))!;
        Assert.Equal(saved.PreparationFingerprint, retained.PreparationFingerprint);
        Assert.NotNull(retained.AcceptedAtUtc);
        Assert.Equal(ProcessLaunchLinkConflictReason.ProjectRetired, retained.LinkConflictReason);
    }

    [Fact]
    public async Task Continuation_recovery_orders_eligible_admissions_and_reclaim_changes_only_lease_generation() {
        await using var app = await TestApplication.CreateAsync(Harness());
        var services = app.Services;
        var clock = new MovingTime();
        var store = new EfProcessPreparedLaunchStore(new ProcessFactory(ProcessOptions(services)), ProcessOptions(services), clock);
        var first = await store.PrepareAsync(ProcessPreparedLaunchFixture.Create());
        var second = await store.PrepareAsync(ProcessPreparedLaunchFixture.Create());
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
        var unitOfWork = new EfProcessRuntimeUnitOfWork(context);
        await unitOfWork.CommitAsync(ProcessPreparedLaunchFixture.Commit(first));
        await unitOfWork.CommitAsync(ProcessPreparedLaunchFixture.Commit(second));
        var claimed = Assert.IsType<ProcessLaunchContinuationClaim>(await store.ClaimContinuationAsync(first.Preparation.AdmissionId));
        Assert.Equal(second.Preparation.AdmissionId, Assert.Single(await store.ListPendingContinuationsAsync(1)));
        clock.Now = clock.Now.AddMinutes(3);
        Assert.Equal(first.Preparation.AdmissionId, Assert.Single(await store.ListPendingContinuationsAsync(1)));
        var reclaimed = Assert.IsType<ProcessLaunchContinuationClaim>(await store.ClaimContinuationAsync(first.Preparation.AdmissionId));
        Assert.Equal(claimed.Generation + 1, reclaimed.Generation);
        Assert.Equal(claimed.Snapshot.PreparationFingerprint, reclaimed.Snapshot.PreparationFingerprint);
        Assert.Equal(claimed.Snapshot.Preparation.InitialCommit.Mutation.State.RunId, reclaimed.Snapshot.Preparation.InitialCommit.Mutation.State.RunId);
        Assert.False(await store.RenewContinuationAsync(claimed));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.CompleteContinuationAsync(claimed, ProcessLaunchContinuationState.Started, null));
    }

    private static TestHarnessOptions Harness(CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null)
        => new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
            foreach (var descriptor in services.Where(item => item.ImplementationType?.Name == "ProcessLaunchContinuationWorker").ToArray()) {
                services.Remove(descriptor);
            }
        } };

    private static ResolvedDatabaseProfile Profile(IServiceProvider services) => services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
    private static CoordinatedDatabaseTransaction Coordinator(IServiceProvider services) => CoordinatedDatabaseTransaction.ForProfile(Profile(services));
    private static DbContextOptions<ProcessPersistenceDbContext> ProcessOptions(IServiceProvider services, params IInterceptor[] interceptors) {
        var builder = new DbContextOptionsBuilder<ProcessPersistenceDbContext>();
        AppDbContextOptionsConfigurator.Configure(builder, Profile(services));
        return builder.AddInterceptors(interceptors).Options;
    }
    private static DbContextOptions<WorkbenchDbContext> WorkbenchOptions(IServiceProvider services, params IInterceptor[] interceptors) {
        var builder = new DbContextOptionsBuilder<WorkbenchDbContext>();
        AppDbContextOptionsConfigurator.Configure(builder, Profile(services));
        return builder.AddInterceptors(interceptors).Options;
    }
    private static DbContextOptions<ProjectsDbContext> ProjectOptions(IServiceProvider services) {
        var builder = new DbContextOptionsBuilder<ProjectsDbContext>();
        AppDbContextOptionsConfigurator.Configure(builder, Profile(services));
        return builder.Options;
    }
    private static EfProcessPreparedLaunchStore Store(IServiceProvider services)
        => new(new ProcessFactory(ProcessOptions(services)), ProcessOptions(services));
    private static ProjectWriteAdmissionService Admissions(IServiceProvider services, CoordinatedDatabaseTransaction coordinator)
        => new(new ProjectFactory(ProjectOptions(services)), ProjectOptions(services), coordinator, services.GetRequiredService<ICanonicalRuntimeDatabase>());
    private static ProjectProcessAdmissionPolicy ProjectPolicy(IServiceProvider services, CoordinatedDatabaseTransaction coordinator)
        => new(Admissions(services, coordinator));
    private static ProjectProcessLaunchTargetQuery Targets(IServiceProvider services, CoordinatedDatabaseTransaction coordinator)
        => new(new WorkbenchFactory(WorkbenchOptions(services)), WorkbenchOptions(services), coordinator, services.GetRequiredService<ProjectWorkbenchService>());
    private static ProjectProcessLaunchDeliveryService Delivery(IServiceProvider services, CoordinatedDatabaseTransaction coordinator,
        DbContextOptions<WorkbenchDbContext>? ownerOptions = null, IProcessLaunchLinkReceiptStore? receiptStore = null)
        => new(new WorkbenchFactory(ownerOptions ?? WorkbenchOptions(services)), coordinator, Admissions(services, coordinator),
            Targets(services, coordinator), services.GetRequiredService<ProjectWorkbenchRelationService>(), Store(services),
            receiptStore ?? new EfProcessPreparedLaunchStore(new ProcessFactory(ProcessOptions(services)), ProcessOptions(services), coordinatedTransaction: coordinator),
            new AuthorityPolicy());

    private static async Task<ProcessProjectAdmission> CreateProjectAsync(IServiceProvider services) {
        var id = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(id, new() { Name = "Process launch receipt fixture" })).IsSuccess);
        var saved = Assert.IsType<ProjectWriteAdmission>(await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(id));
        return new(saved.DatabaseProfileId, saved.ProjectId, saved.LifetimeId);
    }
    private static async Task<ProcessPreparedLaunchSnapshot> CreateAcceptedProjectLaunchAsync(IServiceProvider services) {
        var project = await CreateProjectAsync(services);
        var coordinator = Coordinator(services);
        var target = await Targets(services, coordinator).CaptureAsync(project.ProjectId, ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(project.ProjectId));
        var authority = ProcessPreparedLaunchFixture.Local(project.DatabaseProfileId, project);
        var saved = await Store(services).PrepareAsync(ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()), target));
        await using var context = new ProcessPersistenceDbContext(ProcessOptions(services));
        Assert.True((await new EfProcessRuntimeUnitOfWork(context, coordinatedTransaction: coordinator,
            projectAdmissionPolicy: ProjectPolicy(services, coordinator), launchAuthorityPolicy: new AuthorityPolicy())
            .CommitAsync(ProcessPreparedLaunchFixture.Commit(saved))).Succeeded);
        return (await Store(services).GetAsync(saved.Preparation.AdmissionId))!;
    }
    private static ProcessLaunchRequest Request(ProcessPreparedLaunchSnapshot saved)
        => saved.Preparation.Request with { Authority = saved.Preparation.Authority, ProjectAdmission = saved.Preparation.InitialCommit.Mutation.State.ProjectAdmission,
            LinkTarget = saved.Preparation.LinkTarget, PreparedAdmissionId = saved.Preparation.AdmissionId };
    private static ProcessLaunchApplicationService Service(EfProcessRuntimeUnitOfWork unitOfWork, IProcessPreparedLaunchStore store,
        IProcessLaunchAuthorityPolicy policy, IProcessLaunchArtifactInitializer? artifacts = null)
        => new(null!, new Clock(), null!, null!, null!, unitOfWork, unitOfWork, null!, artifacts!, null!, null!, null!, null!, null!, store, policy);
    private static async Task AssertInitialRowsAsync(ProcessPersistenceDbContext context, ProcessRuntimeCommitRequest request, int expected) {
        var runId = request.Mutation.State.RunId.Value;
        var eventId = request.Mutation.Events.Single().EventId.Value;
        Assert.Equal(expected, await context.RuntimeStates.CountAsync(item => item.RunId == runId));
        Assert.Equal(expected, await context.InstancePlans.CountAsync(item => item.PlanId == request.Mutation.State.PlanId.Value));
        Assert.Equal(expected, await context.RuntimeStepAssignments.CountAsync(item => item.RunId == runId));
        Assert.Equal(expected, await context.RuntimeEvents.CountAsync(item => item.RunId == runId));
        Assert.Equal(expected, await context.OutboxMessages.CountAsync(item => item.EventId == eventId));
        Assert.Equal(expected, await context.ArtifactLedgerEvents.CountAsync(item => item.EventId == eventId));
        Assert.Equal(expected, await context.IdempotencyKeys.CountAsync(item => item.RunId == runId));
    }
    private sealed class ProcessFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }
    private sealed class WorkbenchFactory(DbContextOptions<WorkbenchDbContext> options) : IDbContextFactory<WorkbenchDbContext> {
        public WorkbenchDbContext CreateDbContext() => new(options);
    }
    private sealed class ProjectFactory(DbContextOptions<ProjectsDbContext> options) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => new(options);
    }
    private sealed class Clock : IProcessProjectionClock {
        public DateTimeOffset GetUtcNow() => ProcessProjectAdmissionFixture.Now;
    }
    private sealed class MovingTime : TimeProvider {
        public DateTimeOffset Now { get; set; } = ProcessProjectAdmissionFixture.Now;
        public override DateTimeOffset GetUtcNow() => Now;
    }
    private sealed class ThrowingArtifacts(Exception failure) : IProcessLaunchArtifactInitializer {
        public int Calls { get; private set; }
        public Task InitializeAsync(ProcessLaunchArtifactInitializationRequest request, CancellationToken cancellationToken = default) {
            Calls++;
            throw failure;
        }
    }
    private sealed class AuthorityPolicy : IProcessLaunchAuthorityPolicy {
        public Action? Acquiring { get; set; }
        public Func<ProcessLaunchLinkTarget?, CancellationToken, Task>? Validating { get; set; }
        public bool Held { get; private set; }
        public int Validations { get; private set; }
        public Task<IProcessLaunchAuthorityLease> AcquireAsync(ProcessLaunchAuthority saved, ProcessLaunchAuthority currentCaller, CancellationToken cancellationToken = default) {
            Acquiring?.Invoke();
            Assert.False(Held);
            Held = true;
            return Task.FromResult<IProcessLaunchAuthorityLease>(new Lease(this));
        }
        public Task RequireCurrentAsync(ProcessLaunchAuthority authority, CancellationToken cancellationToken = default) => Task.CompletedTask;
        private sealed class Lease(AuthorityPolicy owner) : IProcessLaunchAuthorityLease {
            public async Task RequireForMutationAsync(ProcessLaunchLinkTarget? linkTarget, CancellationToken cancellationToken = default) {
                Assert.True(owner.Held);
                owner.Validations++;
                if (owner.Validating is { } validate) {
                    await validate(linkTarget, cancellationToken);
                }
            }
            public ValueTask DisposeAsync() {
                owner.Held = false;
                return ValueTask.CompletedTask;
            }
        }
    }
    private sealed class CommitLeaseProbe(AuthorityPolicy policy) : DbTransactionInterceptor {
        public bool Observed { get; private set; }
        public bool CommittedObserved { get; private set; }
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            Assert.True(policy.Held);
            CommittedObserved = true;
            return Task.CompletedTask;
        }
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            Assert.True(policy.Held);
            Observed = true;
            return ValueTask.FromResult(result);
        }
    }
    private sealed class AfterFlushFault(Exception failure) : SaveChangesInterceptor {
        public bool Flushed { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result > 0 && eventData.Context!.Database.CurrentTransaction is not null;
            throw failure;
        }
    }
    private sealed class AfterCommitFault(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) => throw failure;
    }
    private sealed class LinkCommandProbe : DbCommandInterceptor {
        public bool NativeFlushed { get; private set; }
        public bool ReceiptFlushed { get; private set; }
        public DbConnection? Connection { get; private set; }
        public DbTransaction? Transaction { get; private set; }
        public DbConnection? ReceiptConnection { get; private set; }
        public DbTransaction? ReceiptTransaction { get; private set; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("INSERT INTO \"Workbench_ProjectObjectLinks\"", StringComparison.Ordinal)) {
                NativeFlushed = true;
                Connection = command.Connection;
                Transaction = command.Transaction;
            }
            if ((command.CommandText.Contains("UPDATE process_prepared_launches", StringComparison.Ordinal) || command.CommandText.Contains("UPDATE \"process_prepared_launches\"", StringComparison.Ordinal))) {
                ReceiptFlushed = true;
                ReceiptConnection = command.Connection;
                ReceiptTransaction = command.Transaction;
            }
            return ValueTask.FromResult(result);
        }
    }
}
