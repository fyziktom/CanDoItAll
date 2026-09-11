using CanDoItAll.SharedKernel;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed partial class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Source_policy_change_after_actual_flush_rolls_back_native_or_output_admission(bool outputAdmission) {
        var policy = new MutableApiPolicy();
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = services => {
            RemoveAutomaticDelivery(services);
            services.AddSingleton<IOptionsMonitor<ApiAccessOptions>>(policy);
        } });
        await using var fixture = await CreateFixtureAsync(app, prepareOutput: !outputAdmission, sourceSurface: WorkflowStructureOperatorSurface.Api);
        var revoked = new RevokeAfterWorkflowFlush(policy, outputAdmission);
        if (outputAdmission) {
            var options = WorkflowOptions(fixture.Services, revoked);
            var owner = new PersistentWorkflowStructureOutputStore(new WorkflowFactory(options), TimeProvider.System,
                fixture.Services.GetRequiredService<IWorkflowStructureSourceAuthorityPolicy>(),
                fixture.Services.GetRequiredService<CoordinatedDatabaseTransaction>(), options);
            var error = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => owner.PrepareAsync(fixture.Plan));
            Assert.Equal("WorkflowAuthorityDenied", error.ErrorCode);
            Assert.Null(await OutputStore(fixture.Services).FindAsync(fixture.Plan.Identity));
        } else {
            var owner = Owner(fixture.Services, Factory(fixture.Services, revoked));
            var error = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => owner.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request));
            Assert.Equal("WorkflowAuthorityDenied", error.ErrorCode);
            Assert.Null(await fixture.Owner.FindWorkflowContributionAsync(fixture.Plan.Identity));
            await using var read = await fixture.Factory.CreateDbContextAsync();
            Assert.False(await read.Set<ProjectObjectRecord>().AnyAsync(row => row.ProjectId == fixture.ProjectId && row.Title == fixture.Request.Title));
        }
        Assert.True(revoked.SawOwnerRowsInActualTransaction);
        Assert.False(policy.CurrentValue.Enabled);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Save_and_transition_cannot_remove_or_replace_a_runs_original_authority(bool transition, bool remove) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var store = fixture.Services.GetRequiredService<IWorkflowRunStore>();
        var original = Assert.IsType<WorkflowRunSnapshot>(await store.GetRunAsync(fixture.Plan.Identity.Occurrence.RunId));
        var changed = original with { State = WorkflowRunState.Running, Origin = remove ? null : original.Origin! with {
            StructureAuthority = original.Origin.StructureAuthority! with { ProjectScope = new([
                new(fixture.Plan.ProjectLifetime!.DatabaseProfileId, fixture.ProjectId, Guid.NewGuid())], [fixture.ProjectId]) }
        } };
        if (transition) {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.TryTransitionRunAsync(original.RunId, [original.State], changed));
        } else {
            await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveRunAsync(changed));
        }
        var retained = Assert.IsType<WorkflowRunSnapshot>(await store.GetRunAsync(original.RunId));
        Assert.Equal(original.State, retained.State);
        Assert.NotNull(retained.Origin);
        Assert.NotNull(retained.Origin.StructureAuthority);
        Assert.NotNull(retained.Origin.StructureAuthority.ProjectScope);
        Assert.Equal(WorkflowStructureAuthorityFingerprint.Create(original.Origin!.StructureAuthority!),
            WorkflowStructureAuthorityFingerprint.Create(retained.Origin!.StructureAuthority!));
        Assert.Equal(fixture.Plan.ProjectLifetime, retained.Origin.StructureAuthority.ProjectScope!.Find(fixture.ProjectId));
    }

    [Fact]
    public async Task Legacy_runs_remain_observable_but_cannot_be_backfilled_with_current_lifetime_authority() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var store = fixture.Services.GetRequiredService<IWorkflowRunStore>();
        var original = Assert.IsType<WorkflowRunSnapshot>(await store.GetRunAsync(fixture.Plan.Identity.Occurrence.RunId));
        var legacy = original with { RunId = WorkflowRunId.New(), Origin = original.Origin! with {
            StructureAuthority = original.Origin.StructureAuthority! with { ProjectScope = null }
        } };
        await store.SaveRunAsync(legacy);
        await store.SaveRunAsync(legacy with { Summary = "Historical observation" });
        var read = Assert.IsType<WorkflowRunSnapshot>(await store.GetRunAsync(legacy.RunId));
        Assert.Equal("Historical observation", read.Summary);
        Assert.Null(read.Origin!.StructureAuthority!.ProjectScope);
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.SaveRunAsync(read with { Origin = original.Origin }));
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.TryTransitionRunAsync(read.RunId, [read.State], read with { Origin = original.Origin }));
        Assert.Null((await store.GetRunAsync(read.RunId))!.Origin!.StructureAuthority!.ProjectScope);
    }

    [Fact]
    public async Task Independent_output_owners_admit_one_stable_first_project_binding() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app, prepareOutput: false);
        var barrier = new OutputSnapshotBarrier();
        var owners = ConcurrentOutputOwners(fixture.Services, barrier);
        var results = await Task.WhenAll(owners.Select(owner => owner.PrepareAsync(fixture.Plan)));
        Assert.Equal(2, barrier.CapturedSnapshots);
        Assert.True(barrier.StartedTransactions >= 3);
        Assert.All(results, output => Assert.Equal(fixture.Plan.ProjectLifetime, output.Plan.ProjectLifetime));
        Assert.Single(await owners[0].ListAsync(fixture.Plan.Identity.Occurrence.RunId));
        Assert.Equal(fixture.Plan.ProjectLifetime, await owners[1].FindProjectLifetimeAsync(fixture.Plan.Identity.Occurrence.RunId, fixture.ProjectId));
    }

    [Fact]
    public async Task Dynamic_all_project_source_cannot_rebind_a_later_occurrence_to_a_recreated_project_id() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var authority = await fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>()
            .CaptureLocalOperatorAsync(WorkflowStructureOperatorSurface.UserInterface);
        Assert.True(authority.AllProjects);
        Assert.Empty(authority.ProjectScope!.Projects);
        var runId = WorkflowRunId.New();
        await SaveRunAsync(fixture.Services, runId, fixture.Definition,
            new WorkflowLaunchOrigin.Preview(authority.Principal, new("dynamic-target")) { StructureAuthority = authority });
        var first = fixture.Plan with {
            Identity = new(WorkflowExecutionOccurrence.Start(runId).Advance(fixture.Definition.VersionId, new("effect")), 0),
            SourceAuthorityFingerprint = WorkflowStructureAuthorityFingerprint.Create(authority)
        };
        first = first with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(first, fixture.Request) };
        var outputs = OutputStore(fixture.Services);
        await outputs.PrepareAsync(first);
        await fixture.Owner.CreateWorkflowContributionAsync(first, BindRequest(fixture.Request, first, authority));
        var replacement = await RecreateProjectAsync(fixture);
        Assert.NotEqual(first.ProjectLifetime!.LifetimeId, replacement.LifetimeId);
        Assert.Equal(first.ProjectLifetime, await outputs.FindProjectLifetimeAsync(runId, fixture.ProjectId));
        var next = first with { Identity = new(first.Identity.Occurrence.Advance(fixture.Definition.VersionId, new("effect")), 0) };
        next = next with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(next, fixture.Request) };
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => outputs.PrepareAsync(next));
        var retargeted = next with { ProjectLifetime = new(replacement.DatabaseProfileId, replacement.ProjectId, replacement.LifetimeId) };
        retargeted = retargeted with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(retargeted, fixture.Request) };
        await Assert.ThrowsAsync<WorkflowStructureOutputConflictException>(() => outputs.PrepareAsync(retargeted));
        Assert.Single(await outputs.ListAsync(runId));
        Assert.DoesNotContain((await fixture.Owner.GetStructureAsync(fixture.ProjectId)).Nodes, node => node.Title == fixture.Request.Title);
    }

    [Fact]
    public async Task Native_receipt_survives_project_recreation_and_does_not_recreate_removed_output() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var committed = await fixture.Owner.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request);
        _ = await RecreateProjectAsync(fixture);
        var read = Assert.IsType<ProjectWorkflowContributionResult>(await fixture.Owner.FindWorkflowContributionAsync(fixture.Plan.Identity));
        Assert.True(read.TargetDeleted);
        Assert.Equal(committed.Receipt, read.Receipt);
        var replay = await fixture.Owner.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request);
        Assert.True(replay.WasReplay);
        Assert.True(replay.TargetDeleted);
        Assert.Equal(fixture.Plan.ProjectLifetime, replay.Receipt!.ProjectLifetime);
        Assert.DoesNotContain((await fixture.Owner.GetStructureAsync(fixture.ProjectId)).Nodes, node => node.Id == committed.Node.Id);
    }

    [Fact]
    public async Task Recreated_project_blocks_native_status_projection_but_retains_exact_observation() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var node = await fixture.Owner.CreateObjectAsync(fixture.ProjectId,
            new(ProjectObjectType.WorkflowDefinition, "Admitted workflow", "", "", fixture.Request.ParentNodeKey,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() {
                    Workflow = new() { WorkflowId = fixture.Definition.Id, WorkflowVersionId = fixture.Definition.VersionId }
                })));
        var authority = fixture.Request.WorkflowMutationAdmission!.Authority;
        var actor = new ProjectStructureAgentContext("operator", "Fixture", "fixture", "", "", "captured-source") {
            WorkflowAuthority = ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface)
        };
        var admission = await fixture.Owner.PrepareWorkflowAdmissionAsync(fixture.ProjectId, node.Id, Guid.NewGuid(), true,
            fixture.Definition, "{}", null, WorkflowPreviewSimulationPlan.Empty, authority, actor);
        Assert.Equal(admission.Binding.IntentId, (await fixture.Owner.FindSelectedWorkflowAdmissionAsync(fixture.ProjectId, node.Id))!.Binding.IntentId);
        var run = await SaveRunAsync(fixture.Services, admission.Binding.RunId, fixture.Definition, admission.LaunchIntent.Origin);
        _ = await RecreateProjectAsync(fixture);
        var state = await fixture.Owner.DeliverWorkflowStatusAsync(admission, Status(run), run, true);
        Assert.Equal(ProjectWorkflowDeliveryState.TargetDeleted, state);
        var saved = Assert.IsType<ProjectWorkflowAdmission>(await fixture.Owner.FindWorkflowAdmissionAsync(admission.Binding.IntentId));
        Assert.Equal(run.RunId, saved.RecordedStatus!.RunId);
        Assert.Equal(admission.Binding.Sequence, saved.RecordedStatus.AdmissionSequence);
        Assert.Equal(fixture.Plan.ProjectLifetime, saved.Binding.Authority.ProjectScope!.Find(fixture.ProjectId));
        Assert.Null(await fixture.Owner.FindSelectedWorkflowAdmissionAsync(fixture.ProjectId, node.Id));
        Assert.DoesNotContain((await fixture.Owner.GetStructureAsync(fixture.ProjectId)).Nodes, current => current.Id == node.Id);
    }

    [Fact]
    public async Task Global_workflow_captures_only_fixed_executor_targets_at_admission() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var source = fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>();
        var authority = await source.CaptureLocalOperatorAsync(WorkflowStructureOperatorSurface.UserInterface);
        var effect = new WorkflowNode(new("effect"), WorkflowNodeKind.Executor, "Create task", [],
            new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text) {
                ExecutorId = WorkflowExecutorIds.ProjectStructure,
                ExecutorSettingsJson = JsonSerializer.Serialize(new WorkflowProjectStructureExecutorSettings { Operation = WorkflowProjectStructureOperation.CreateTaskNodes,
                    ProjectId = fixture.ProjectId }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
        var definition = fixture.Definition with { Graph = fixture.Definition.Graph with { Nodes = fixture.Definition.Graph.Nodes.Append(effect).ToArray() } };
        var captured = await source.PrepareLaunchAsync(authority, definition);
        Assert.Equal(fixture.Plan.ProjectLifetime, Assert.Single(captured.ProjectScope!.Projects));
        Assert.Equal(fixture.ProjectId, Assert.Single(captured.ProjectScope.AdmissionProjectIds));
        Assert.Empty(authority.ProjectScope!.Projects);
        _ = await RecreateProjectAsync(fixture);
        var replayPreparation = await source.PrepareLaunchAsync(captured, definition);
        Assert.Equal(fixture.Plan.ProjectLifetime, Assert.Single(replayPreparation.ProjectScope!.Projects));
        await using var context = await fixture.Factory.CreateDbContextAsync();
        await using var transaction = await context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
        using var coordination = fixture.Services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(context);
        await using var lease = await source.AcquireAsync(captured, WorkflowStructureAuthorityUse.Admission);
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => lease.RequireForMutationAsync());
    }

    private static async Task<ProjectWriteAdmission> RecreateProjectAsync(Fixture fixture) {
        var original = fixture.Plan.ProjectLifetime!;
        var projects = fixture.Services.GetRequiredService<ProjectsService>();
        var deleted = await projects.DeleteAsync(fixture.ProjectId, expectedProjectAdmission:
            new(original.DatabaseProfileId, original.ProjectId, original.LifetimeId));
        Assert.Equal(fixture.ProjectId, deleted.ProjectId);
        await using var owner = await fixture.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        owner.Add(new Project { Id = fixture.ProjectId, Name = "Replacement project", Slug = $"replacement-{Guid.NewGuid():N}" });
        await owner.SaveChangesAsync();
        return Assert.IsType<ProjectWriteAdmission>(await fixture.Services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(fixture.ProjectId));
    }

    private static DbContextOptions<WorkflowDbContext> WorkflowOptions(IServiceProvider services, params IInterceptor[] interceptors) {
        var options = new DbContextOptionsBuilder<WorkflowDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        options.AddInterceptors(interceptors);
        return options.Options;
    }

    private sealed class WorkflowFactory(DbContextOptions<WorkflowDbContext> options) : IDbContextFactory<WorkflowDbContext> {
        public WorkflowDbContext CreateDbContext() => new(options);
        public Task<WorkflowDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class MutableApiPolicy : IOptionsMonitor<ApiAccessOptions> {
        public ApiAccessOptions CurrentValue { get; } = new() { Enabled = true };
        public ApiAccessOptions Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<ApiAccessOptions, string?> listener) => null;
    }

    private sealed class RevokeAfterWorkflowFlush(MutableApiPolicy policy, bool outputAdmission) : SaveChangesInterceptor {
        public bool SawOwnerRowsInActualTransaction { get; private set; }

        public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (SawOwnerRowsInActualTransaction) {
                return result;
            }
            if (outputAdmission && eventData.Context is WorkflowDbContext workflow &&
                    workflow.ChangeTracker.Entries<WorkflowStructureOutputRecord>().Any()) {
                Assert.NotNull(workflow.Database.CurrentTransaction);
                SawOwnerRowsInActualTransaction = await workflow.Set<WorkflowStructureOutputRecord>().AnyAsync(cancellationToken);
            } else if (!outputAdmission && eventData.Context is WorkbenchDbContext native &&
                    native.ChangeTracker.Entries<ProjectWorkflowContributionRecord>().Any(entry => entry.Entity.ReceiptJson != "")) {
                Assert.NotNull(native.Database.CurrentTransaction);
                var contribution = native.ChangeTracker.Entries<ProjectWorkflowContributionRecord>().Single().Entity;
                SawOwnerRowsInActualTransaction = await native.Set<ProjectWorkflowContributionRecord>().AnyAsync(row =>
                    row.RunId == contribution.RunId && row.ReceiptJson != "", cancellationToken) &&
                    await native.Set<ProjectObjectRecord>().AnyAsync(row => row.Id == contribution.NativeObjectId, cancellationToken);
            }
            if (SawOwnerRowsInActualTransaction) {
                policy.CurrentValue.Enabled = false;
            }
            return result;
        }
    }
}
