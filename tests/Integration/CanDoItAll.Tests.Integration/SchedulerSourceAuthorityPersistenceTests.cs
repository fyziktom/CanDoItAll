using System.Data.Common;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.SchedulerPlanner;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration;

public sealed class SchedulerSourceAuthorityPersistenceTests {
    [Fact]
    public async Task Scheduled_Workflow_admission_holds_source_and_plan_until_commit_on_the_exact_shared_transaction() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var query = new AdmissionQueryProbe();
        var commit = new AdmissionCommitProbe(fixture);
        var store = fixture.Store([query, commit], [query]);
        await store.CreateRunWithStartedEventAsync(fixture.Run, fixture.Started);
        Assert.True(commit.BeforeCommit);
        Assert.True(commit.AfterCommit);
        Assert.NotNull(query.WorkflowTransaction);
        Assert.Same(query.WorkflowTransaction, query.SchedulerTransaction);
        Assert.Same(query.WorkflowConnection, query.SchedulerConnection);
        await fixture.RevokeAgentAsync(AgentRevocation.Schedule);
        await fixture.SetPlanEnabledAsync(false);
        Assert.Equal(fixture.Run.RunId, (await store.GetRunAsync(fixture.Run.RunId))!.RunId);
        Assert.Single(await store.ListEventsAsync(fixture.Run.RunId));
    }

    [Fact]
    public async Task Backend_execution_begins_after_source_lease_and_admission_transaction_are_released() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var backend = new CatalogEditingBackend(fixture);
        var manager = WorkflowRuntimeManager.CreateInMemory([backend], fixture.Store());
        var result = await manager.StartAsync(fixture.Definition, new(fixture.Definition.Id, fixture.Definition.VersionId, "{}",
            WorkflowRuntimeBackendKind.InProcess, null, null) {
            Origin = fixture.Run.Origin, RequestedRunId = fixture.Run.RunId
        });
        Assert.True(backend.Entered);
        Assert.Equal(fixture.Run.RunId, result.RunId);
        Assert.Equal(WorkflowRunState.Completed, result.State);
        Assert.Single((await fixture.Store().ListEventsAsync(result.RunId)).Where(item => item.Kind == WorkflowEventKind.Started));
    }

    public enum AgentRevocation { Tools, Schedule, Inactive }

    [Theory]
    [InlineData(AgentRevocation.Tools)]
    [InlineData(AgentRevocation.Schedule)]
    [InlineData(AgentRevocation.Inactive)]
    public async Task Current_source_revocation_blocks_new_Workflow_admission_without_losing_prepared_fire(AgentRevocation revocation) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        await fixture.RevokeAgentAsync(revocation);
        await Assert.ThrowsAsync<WorkflowScheduledSourceAuthorityException>(() => fixture.Store()
            .CreateRunWithStartedEventAsync(fixture.Run, fixture.Started));
        await fixture.AssertNoWorkflowAdmissionAsync();
        await using var scheduler = fixture.SchedulerContext();
        var retained = await scheduler.Set<SchedulerFireAdmissionRecord>().SingleAsync(row => row.Id == fixture.Claim.Snapshot.PlanRunId);
        Assert.Equal(fixture.Run.RunId.Value, retained.PreparedWorkflowRunId);
        Assert.Null(retained.AcceptedWorkflowRunId);
        Assert.Equal(fixture.Claim.Snapshot.ToJson(), retained.SnapshotJson);
    }

    public enum PlanRevocation { Disabled, AuthorityChanged, Deleted }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Saved_schedule_operation_and_current_profile_generation_cannot_be_widened(bool wrongGeneration) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var origin = Assert.IsType<WorkflowLaunchOrigin.SchedulerPlanRun>(fixture.Run.Origin);
        var authority = origin.StructureAuthority!;
        var governance = authority.AgentGovernance!;
        var changed = new AgentExecutionGovernanceSnapshot(governance.AuthorityId, governance.AgentId, governance.DatabaseProfileId,
            wrongGeneration ? new(governance.DatabaseProfileGeneration.Value + 1) : governance.DatabaseProfileGeneration,
            governance.WorkspaceScope, governance.ReadAllowed, governance.MutationAllowed, governance.PolicyVersion, governance.PolicyFingerprint,
            wrongGeneration ? governance.AllowedOperations.ToArray() : ["unrelated-fixture-operation"]);
        var run = fixture.Run with { Origin = origin with { StructureAuthority = authority with { AgentGovernance = changed } } };
        await Assert.ThrowsAsync<WorkflowScheduledSourceAuthorityException>(() => fixture.Store().CreateRunWithStartedEventAsync(run, fixture.Started));
        await fixture.AssertNoWorkflowAdmissionAsync();
    }

    [Theory]
    [InlineData(PlanRevocation.Disabled)]
    [InlineData(PlanRevocation.AuthorityChanged)]
    [InlineData(PlanRevocation.Deleted)]
    public async Task Current_plan_revocation_is_checked_inside_the_initial_Workflow_transaction(PlanRevocation revocation) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        await using (var scheduler = fixture.SchedulerContext()) {
            var plan = await scheduler.Set<SchedulerPlan>().SingleAsync(row => row.Id == fixture.Plan.Id);
            if (revocation == PlanRevocation.Deleted) {
                scheduler.Remove(plan);
            } else if (revocation == PlanRevocation.Disabled) {
                plan.IsEnabled = false;
            } else {
                plan.StructureAuthorityJson = null;
            }
            await scheduler.SaveChangesAsync();
        }
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => fixture.Store()
            .CreateRunWithStartedEventAsync(fixture.Run, fixture.Started));
        await fixture.AssertNoWorkflowAdmissionAsync();
        await using var released = await fixture.Writer.AcquireAgentReadLeaseAsync(fixture.Agent.Id);
        Assert.Equal(fixture.Agent.Id, released.Agent!.Id);
    }

    [Fact]
    public async Task Actual_post_flush_failure_rolls_back_run_and_event_and_releases_both_locks() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var failure = new ArgumentException("Injected failure after actual Workflow and Started event flush.");
        var probe = new FlushFault(failure);
        Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store([probe])
            .CreateRunWithStartedEventAsync(fixture.Run, fixture.Started)));
        Assert.True(probe.Flushed);
        await fixture.AssertNoWorkflowAdmissionAsync();
        await fixture.RevokeAgentAsync(AgentRevocation.Schedule);
        await fixture.SetPlanEnabledAsync(false);
    }

    [Fact]
    public async Task Lost_commit_ack_preserves_exact_exception_and_existing_run_replay_after_source_revocation() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var failure = new ArgumentException("Injected acknowledgement loss after real scheduled Workflow admission.");
        Assert.Same(failure, await Assert.ThrowsAsync<ArgumentException>(() => fixture.Store([new CommitFault(failure)])
            .CreateRunWithStartedEventAsync(fixture.Run, fixture.Started)));
        await fixture.RevokeAgentAsync(AgentRevocation.Schedule);
        await fixture.SetPlanEnabledAsync(false);
        var replay = await Assert.ThrowsAsync<WorkflowRunAlreadyExistsException>(() => fixture.Store()
            .CreateRunWithStartedEventAsync(fixture.Run, fixture.Started));
        Assert.Equal(fixture.Run.RunId, replay.RunId);
        var saved = (await fixture.Store().GetRunAsync(fixture.Run.RunId))!;
        Assert.Equal(fixture.Run.RunId, saved.RunId);
        Assert.Equal(JsonSerializer.Serialize(fixture.Run.Origin), JsonSerializer.Serialize(saved.Origin));
        Assert.Single(await fixture.Store().ListEventsAsync(saved.RunId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Missing_trusted_scheduler_source_or_policy_fails_before_new_writes_while_ordinary_runs_remain_supported(bool missingPolicy) {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var run = missingPolicy ? fixture.Run : fixture.Run with {
            Origin = Assert.IsType<WorkflowLaunchOrigin.SchedulerPlanRun>(fixture.Run.Origin) with { StructureAuthority = null }
        };
        var store = missingPolicy ? new PersistentWorkflowRunStore(fixture.WorkflowFactory()) : fixture.Store();
        if (missingPolicy) {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.CreateRunWithStartedEventAsync(run, fixture.Started));
        } else {
            await Assert.ThrowsAsync<WorkflowScheduledSourceAuthorityException>(() => store.CreateRunWithStartedEventAsync(run, fixture.Started));
        }
        await fixture.AssertNoWorkflowAdmissionAsync();
        var ordinary = run with { RunId = WorkflowRunId.New(), Origin = null };
        await new PersistentWorkflowRunStore(fixture.WorkflowFactory()).CreateRunWithStartedEventAsync(ordinary,
            fixture.Started with { Id = Guid.NewGuid(), RunId = ordinary.RunId });
        Assert.Equal(ordinary.RunId, (await store.GetRunAsync(ordinary.RunId))!.RunId);
    }

    [Fact]
    public async Task Saved_schedule_requires_current_source_through_owner_save_and_releases_before_trigger_callback() {
        await using var app = await TestApplication.CreateAsync(Harness());
        await using var scope = app.Services.CreateAsyncScope();
        var fixture = await Fixture.CreateAsync(scope.ServiceProvider);
        var afterSave = new SaveCatalogProbe(fixture.Writer);
        var trigger = new CatalogEditingTrigger(fixture);
        var service = fixture.Planner(trigger, [afterSave]);
        var editor = await service.GetPlanEditorAsync(fixture.Plan.Id);
        editor.StructureAuthority = fixture.Claim.Snapshot.Authority;
        editor.Name = "Reviewed scheduled source";
        var saved = await service.SavePlanAsync(editor);
        Assert.True(afterSave.Observed);
        Assert.True(trigger.Called);
        Assert.Equal(editor.Name, saved.Name);
        editor.Name = "Denied after revocation";
        await Assert.ThrowsAsync<WorkflowScheduledSourceAuthorityException>(() => service.SavePlanAsync(editor));
        await using var read = fixture.SchedulerContext();
        Assert.Equal(saved.Name, (await read.Set<SchedulerPlan>().SingleAsync(row => row.Id == saved.Id)).Name);
    }

    [Fact]
    public async Task Restart_uses_saved_nonempty_source_and_fire_identity_for_exact_prepared_Workflow_admission() {
        await using var environment = CanDoItAllTestEnvironment.Create("scheduler-source-admission-restart");
        var profile = environment.CreatePostgreSqlProfile("saved");
        var options = Harness(environment, profile);
        string savedRun;
        string savedStarted;
        Guid agentId;
        Guid authorityId;
        await using (var first = await TestApplication.CreateAsync(options)) {
            await using var firstScope = first.Services.CreateAsyncScope();
            var fixture = await Fixture.CreateAsync(firstScope.ServiceProvider);
            savedRun = JsonSerializer.Serialize(fixture.Run);
            savedStarted = JsonSerializer.Serialize(fixture.Started);
            agentId = fixture.Agent.Id;
            authorityId = fixture.Run.Origin!.StructureAuthority!.AgentGovernance!.AuthorityId.Value;
        }
        await using var restarted = await TestApplication.CreateAsync(options);
        await using var scope = restarted.Services.CreateAsyncScope();
        var run = JsonSerializer.Deserialize<WorkflowRunSnapshot>(savedRun)!;
        var started = JsonSerializer.Deserialize<WorkflowEventRecord>(savedStarted)!;
        var resumed = await Fixture.ResumeAsync(scope.ServiceProvider, run, started);
        Assert.Equal(agentId, run.Origin!.StructureAuthority!.AgentGovernance!.AgentId);
        Assert.NotEqual(Guid.Empty, authorityId);
        Assert.Equal(authorityId, run.Origin.StructureAuthority.AgentGovernance.AuthorityId.Value);
        await resumed.Store().CreateRunWithStartedEventAsync(run, started);
        Assert.Equal(run.RunId, (await resumed.Store().GetRunAsync(run.RunId))!.RunId);
        Assert.Single(await resumed.Store().ListEventsAsync(run.RunId));
        Assert.Equal(savedRun, JsonSerializer.Serialize(run));
    }

    private static TestHarnessOptions Harness(CanDoItAllTestEnvironment? environment = null, TestDatabaseProfile? profile = null)
        => new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = services => {
            foreach (var descriptor in services.Where(item => item.ImplementationType == typeof(SchedulerFireRecoveryWorker)).ToArray()) {
                services.Remove(descriptor);
            }
        } };

    private sealed class Fixture(IServiceProvider services) {
        public SchedulerPlan Plan { get; private set; } = null!;
        public SchedulerFireClaim Claim { get; private set; } = null!;
        public WorkflowRunSnapshot Run { get; private set; } = null!;
        public WorkflowEventRecord Started { get; private set; } = null!;
        public AgentDefinition Agent { get; private set; } = null!;
        public FileSandboxWorkspaceStore Writer { get; private set; } = null!;
        public CanonicalAgentCatalogLeaseSource Source { get; private set; } = null!;
        public WorkflowDefinition Definition { get; private set; } = null!;
        public InMemoryWorkflowCatalogService Catalog { get; private set; } = null!;
        public CoordinatedDatabaseTransaction Coordinator { get; } = CoordinatedDatabaseTransaction.ForProfile(
            services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);

        public static async Task<Fixture> CreateAsync(IServiceProvider services) {
            var fixture = new Fixture(services);
            var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>();
            var projects = services.GetRequiredService<ProjectWriteAdmissionService>();
            fixture.Source = new(profile, services.GetRequiredService<IOptions<StorageOptions>>(), services.GetRequiredService<IHostEnvironment>(), projects);
            fixture.Writer = new(profile.Profile.Profile.Storage.WorkspaceRoot,
                WorkspaceScopeDescriptor.Organization(profile.Profile.Profile.Id.ToString("N")), new AgentProjectAccessCatalogPolicy(projects, profile.Profile.Profile.Id));
            var current = await fixture.Writer.LoadCatalogAsync();
            fixture.Agent = current.Agents.First() with { Id = Guid.NewGuid(), Name = "Scheduled source fixture", IsTemplate = false,
                TemplateKey = string.Empty, Tags = [], ConfigurationJson = "{}", Status = AgentLifecycleStatus.Active,
                Permissions = AgentPermissionsPolicy.Default with { CanUseTools = true, CanScheduleWork = true } };
            await fixture.Writer.UpdateCatalogAsync(catalog => catalog with { Agents = [.. catalog.Agents, fixture.Agent] });
            var governance = new AgentExecutionGovernanceSnapshot(new(Guid.NewGuid()), fixture.Agent.Id, profile.Profile.Profile.Id,
                services.GetRequiredService<IAgentExecutionProfileGenerationSource>().GetGeneration(),
                WorkspaceScopeDescriptor.Organization(profile.Profile.Profile.Id.ToString("N")), true, true,
                "saved-scheduler-policy", "saved-scheduler-fingerprint", [SchedulerToolPolicy.SchedulerWorkflowScheduleCreate]);
            var authority = new WorkflowStructureAuthority(WorkflowStructureAuthorityChannel.AgentExecution,
                new(WorkflowLaunchActorKind.Agent, fixture.Agent.Id.ToString("D")), profile.Profile.Profile.Id, Guid.Empty,
                false, false, null, governance.PolicyFingerprint) { AgentGovernance = governance };
            fixture.Definition = NewDefinition();
            fixture.Catalog = new(services.GetRequiredService<IWorkflowDefinitionValidator>());
            fixture.Definition = await fixture.Catalog.SaveDefinitionAsync(new(fixture.Definition.Id, null,
                fixture.Definition.Name, fixture.Definition.Description, fixture.Definition.Status,
                fixture.Definition.Graph, fixture.Definition.RuntimePolicy));
            fixture.Plan = new() { Id = Guid.NewGuid(), Name = "Authority fence", TargetKind = SchedulerPlanTargetKind.Workflow,
                TargetId = fixture.Definition.Id.Value, TargetVersionId = fixture.Definition.VersionId.Value, TargetNameSnapshot = fixture.Definition.Name,
                SchedulerTriggerId = Guid.NewGuid(), SchedulerTriggerKey = $"authority-fixture-{Guid.NewGuid():N}",
                CronExpression = "0 0/30 * * * ?", CronDescription = "Every thirty minutes", InputJson = "{}",
                StructureAuthorityJson = SchedulerFireSnapshot.SerializeAuthority(authority), IsEnabled = true };
            await using (var scheduler = fixture.SchedulerContext()) {
                scheduler.Add(fixture.Plan);
                await scheduler.SaveChangesAsync();
            }
            var admissions = new SchedulerFireAdmissionStore(new Factory<SchedulerPlannerDbContext>(fixture.Options<SchedulerPlannerDbContext>(), static value => new(value)),
                fixture.Catalog, services.GetRequiredService<IWorkflowRuntimeManager>(), services.GetRequiredService<IClock>());
            fixture.Claim = (await admissions.AcquireAsync(new(fixture.Plan.Id, Guid.NewGuid(), Guid.NewGuid(),
                new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.Zero), null)))!;
            var context = fixture.Claim.Snapshot.ToContext(fixture.Claim.PreparedRunId, true);
            var origin = new WorkflowLaunchOrigin.SchedulerPlanRun(context.PlanId, context.PlanRunId, context.SchedulerFireId,
                context.FiredAtUtc, context.CorrelationId) { PreparedRunId = context.PreparedRunId, StructureAuthority = context.StructureAuthority };
            var now = DateTimeOffset.UtcNow;
            fixture.Run = new(new(fixture.Claim.PreparedRunId), fixture.Definition.Id, fixture.Definition.VersionId,
                WorkflowRunState.Running, WorkflowRuntimeBackendKind.InProcess, "scheduled-admission", "Started", now, now) { Origin = origin };
            fixture.Started = new(Guid.NewGuid(), fixture.Run.RunId, WorkflowEventKind.Started, null, "Scheduled admission", "{}", now);
            return fixture;
        }

        public static async Task<Fixture> ResumeAsync(IServiceProvider services, WorkflowRunSnapshot run, WorkflowEventRecord started) {
            var fixture = new Fixture(services) { Run = run, Started = started };
            var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>();
            var projects = services.GetRequiredService<ProjectWriteAdmissionService>();
            fixture.Source = new(profile, services.GetRequiredService<IOptions<StorageOptions>>(), services.GetRequiredService<IHostEnvironment>(), projects);
            fixture.Writer = new(profile.Profile.Profile.Storage.WorkspaceRoot,
                WorkspaceScopeDescriptor.Organization(profile.Profile.Profile.Id.ToString("N")), new AgentProjectAccessCatalogPolicy(projects, profile.Profile.Profile.Id));
            await using (var held = await fixture.Writer.AcquireAgentReadLeaseAsync(run.Origin!.StructureAuthority!.AgentGovernance!.AgentId)) {
                fixture.Agent = Assert.IsType<AgentDefinition>(held.Agent);
            }
            await using var scheduler = fixture.SchedulerContext();
            var origin = Assert.IsType<WorkflowLaunchOrigin.SchedulerPlanRun>(run.Origin);
            fixture.Plan = await scheduler.Set<SchedulerPlan>().SingleAsync(row => row.Id == origin.PlanId);
            var admission = await scheduler.Set<SchedulerFireAdmissionRecord>().SingleAsync(row => row.Id == origin.PlanRunId);
            var snapshot = SchedulerFireSnapshot.Parse(admission.SnapshotJson);
            Assert.Equal(admission.SnapshotFingerprint, snapshot.Fingerprint());
            Assert.Equal(run.RunId.Value, admission.PreparedWorkflowRunId);
            fixture.Claim = new(snapshot, admission.PreparedWorkflowRunId, admission.LeaseOwner!.Value, admission.Generation, true);
            return fixture;
        }

        public PersistentWorkflowRunStore Store(IInterceptor[]? workflowInterceptors = null, IInterceptor[]? schedulerInterceptors = null)
            => new(WorkflowFactory(workflowInterceptors), Policy(schedulerInterceptors), Coordinator);

        public IDbContextFactory<WorkflowDbContext> WorkflowFactory(IInterceptor[]? interceptors = null)
            => new Factory<WorkflowDbContext>(Options<WorkflowDbContext>(interceptors), static value => new(value));

        public SchedulerPlannerDbContext SchedulerContext() => new(Options<SchedulerPlannerDbContext>());

        public SchedulerPlannerService Planner(ISchedulerPlannerTriggerScheduler triggers, IInterceptor[]? interceptors = null)
            => new(new Factory<SchedulerPlannerDbContext>(Options<SchedulerPlannerDbContext>(interceptors), static value => new(value)), triggers,
                new QuartzCronDescriptionService(), Catalog, new SchedulerWorkflowInputSchemaService(Catalog),
                services.GetRequiredService<IClock>(), NullLogger<SchedulerPlannerService>.Instance, Policy());

        private ProjectScheduledWorkflowSourceAuthorityPolicy Policy(IInterceptor[]? interceptors = null)
            => new(services.GetRequiredService<ICanonicalRuntimeDatabase>(), Source, services.GetRequiredService<IAgentExecutionProfileGenerationSource>(),
                new SchedulerWorkflowAuthorityPolicy(new Factory<SchedulerPlannerDbContext>(Options<SchedulerPlannerDbContext>(interceptors), static value => new(value)),
                    Options<SchedulerPlannerDbContext>(interceptors), Coordinator), services.GetRequiredService<IOptionsMonitor<ApiAccessOptions>>(), TimeProvider.System);

        public Task<SandboxWorkspaceCatalog> RevokeAgentAsync(AgentRevocation change, CancellationToken cancellationToken = default)
            => Writer.UpdateCatalogAsync(catalog => catalog with { Agents = catalog.Agents.Select(agent => agent.Id != Agent.Id ? agent : change switch {
                AgentRevocation.Tools => agent with { Permissions = agent.Permissions with { CanUseTools = false } },
                AgentRevocation.Schedule => agent with { Permissions = agent.Permissions with { CanScheduleWork = false } },
                AgentRevocation.Inactive => agent with { Status = AgentLifecycleStatus.Suspended },
                _ => throw new ArgumentOutOfRangeException(nameof(change))
            }).ToArray() }, cancellationToken);

        public async Task SetPlanEnabledAsync(bool enabled, CancellationToken cancellationToken = default) {
            await using var database = SchedulerContext();
            var plan = await database.Set<SchedulerPlan>().SingleAsync(row => row.Id == Plan.Id, cancellationToken);
            plan.IsEnabled = enabled;
            await database.SaveChangesAsync(cancellationToken);
        }

        public async Task AssertNoWorkflowAdmissionAsync() {
            await using var database = await WorkflowFactory().CreateDbContextAsync();
            Assert.False(await database.Set<WorkflowRunRecordEntity>().AnyAsync(row => row.RunId == Run.RunId.Value));
            Assert.False(await database.Set<WorkflowEventRecordEntity>().AnyAsync(row => row.RunId == Run.RunId.Value));
        }

        private DbContextOptions<T> Options<T>(IInterceptor[]? interceptors = null) where T : DbContext {
            var builder = new DbContextOptionsBuilder<T>();
            AppDbContextOptionsConfigurator.Configure(builder, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
            return builder.AddInterceptors(interceptors ?? []).Options;
        }
    }

    private static WorkflowDefinition NewDefinition() {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        static WorkflowNode Node(WorkflowNodeId id, WorkflowNodeKind kind) => new(id, kind, id.Value, [],
            new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text));
        return new(WorkflowId.New(), WorkflowVersionId.New(), "Scheduled authority fixture", "Admission boundary proof", WorkflowLifecycleStatus.Draft,
            new(start, [Node(start, WorkflowNodeKind.Start), Node(end, WorkflowNodeKind.End)],
                [new(new("start-end"), start, null, end, null, WorkflowEdgeKind.Direct, string.Empty) { Routing = WorkflowEdgeRouting.Always }]),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private sealed class Factory<T>(DbContextOptions<T> options, Func<DbContextOptions<T>, T> create) : IDbContextFactory<T> where T : DbContext {
        public T CreateDbContext() => create(options);
    }

    private sealed class AdmissionQueryProbe : DbCommandInterceptor {
        public DbTransaction? WorkflowTransaction { get; private set; }
        public DbConnection? WorkflowConnection { get; private set; }
        public DbTransaction? SchedulerTransaction { get; private set; }
        public DbConnection? SchedulerConnection { get; private set; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (command.CommandText.Contains("INSERT INTO \"AgentFramework_WorkflowRuns\"", StringComparison.Ordinal)) {
                WorkflowTransaction = command.Transaction;
                WorkflowConnection = command.Connection;
            }
            if (command.CommandText.Contains("FOR SHARE", StringComparison.Ordinal)) {
                SchedulerTransaction = command.Transaction;
                SchedulerConnection = command.Connection;
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class AdmissionCommitProbe(Fixture fixture) : DbTransactionInterceptor {
        public bool BeforeCommit { get; private set; }
        public bool AfterCommit { get; private set; }
        public override async ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
            InterceptionResult result, CancellationToken cancellationToken = default) {
            await RequireCatalogHeldAsync(fixture.Writer);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.SetPlanEnabledAsync(false, timeout.Token));
            BeforeCommit = true;
            return result;
        }
        public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            await RequireCatalogHeldAsync(fixture.Writer);
            AfterCommit = true;
        }
    }

    private static async Task RequireCatalogHeldAsync(FileSandboxWorkspaceStore writer) {
        using var timeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => writer.UpdateCatalogAsync(catalog => catalog, timeout.Token));
    }

    private sealed class FlushFault(Exception failure) : SaveChangesInterceptor {
        public bool Flushed { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            Flushed = result == 2 && eventData.Context!.Database.CurrentTransaction is not null;
            throw failure;
        }
    }
    private sealed class CommitFault(Exception failure) : DbTransactionInterceptor {
        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) => throw failure;
    }
    private sealed class SaveCatalogProbe(FileSandboxWorkspaceStore writer) : SaveChangesInterceptor {
        public bool Observed { get; private set; }
        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            await RequireCatalogHeldAsync(writer);
            Observed = result == 1;
            return result;
        }
    }
    private sealed class CatalogEditingTrigger(Fixture fixture) : ISchedulerPlannerTriggerScheduler {
        public bool Called { get; private set; }
        public Task SynchronizeAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException("Only selected-plan synchronization is expected.");
        public async Task SynchronizePlanAsync(Guid planId, CancellationToken cancellationToken = default) {
            Assert.Equal(fixture.Plan.Id, planId);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await fixture.RevokeAgentAsync(AgentRevocation.Schedule, timeout.Token);
            Called = true;
        }
    }
    private sealed class CatalogEditingBackend(Fixture fixture) : IWorkflowExecutionBackend {
        public bool Entered { get; private set; }
        public WorkflowRuntimeBackendDescriptor Descriptor { get; } = new(WorkflowRuntimeBackendKind.InProcess,
            "Catalog lease boundary fixture", false, false, false, false, "No provider calls.");
        public async Task<WorkflowBackendStartResult> StartAsync(WorkflowDefinition definition, WorkflowRunStartRequest request,
            WorkflowRunId runId, CancellationToken cancellationToken = default) {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await fixture.RevokeAgentAsync(AgentRevocation.Schedule, timeout.Token);
            await fixture.SetPlanEnabledAsync(false, timeout.Token);
            Entered = true;
            return new(fixture.Run with { RunId = runId, State = WorkflowRunState.Completed, UpdatedAtUtc = DateTimeOffset.UtcNow }, [], [], []);
        }
    }
}
