using System.Data.Common;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.ProjectStructure;

public sealed class WorkflowStructureDeliveryPersistenceTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NativeMutationAndReceiptShareCommitAndRecoverLostAcknowledgement(bool afterCommit) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var fault = new CommitFault(afterCommit);
        var failing = Owner(fixture.Services, Factory(fixture.Services, new ObserveReceiptCommands(fault), fault));

        var thrown = await Assert.ThrowsAsync<ArgumentException>(() => failing.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request));
        Assert.Same(fault.Failure, thrown);
        Assert.True(fault.SawReceiptInsert);
        await using (var database = await fixture.Factory.CreateDbContextAsync()) {
            Assert.Equal(afterCommit ? 1 : 0, await database.Set<ProjectWorkflowContributionRecord>()
                .CountAsync(row => row.RunId == fixture.Plan.Identity.Occurrence.RunId.Value && row.ReceiptJson != ""));
            Assert.Equal(afterCommit ? 1 : 0, await database.Set<ProjectObjectRecord>()
                .CountAsync(row => row.ProjectId == fixture.ProjectId && row.Title == fixture.Request.Title));
        }

        var prepared = await fixture.Owner.FindPreparedWorkflowContributionAsync(fixture.Plan.Identity);
        Assert.NotNull(prepared);
        Assert.Equal(fixture.Plan, prepared.Plan);
        Assert.Equal(fixture.Plan.Fingerprint, ProjectWorkflowContributionFingerprint.Create(prepared.Plan, prepared.Request));
        var recovered = await fixture.Owner.FindWorkflowContributionAsync(fixture.Plan.Identity);
        Assert.Equal(afterCommit, recovered is not null);
        var replay = await fixture.Owner.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request);
        Assert.Equal(afterCommit, replay.WasReplay);
        if (afterCommit) {
            Assert.Equal(recovered!.Receipt, replay.Receipt);
            Assert.Equal(recovered.Node.Id, replay.Node.Id);
        }

        await using var verified = await fixture.Factory.CreateDbContextAsync();
        Assert.Single(await verified.Set<ProjectWorkflowContributionRecord>()
            .Where(row => row.RunId == fixture.Plan.Identity.Occurrence.RunId.Value).ToListAsync());
        Assert.Single(await verified.Set<ProjectObjectRecord>()
            .Where(row => row.ProjectId == fixture.ProjectId && row.Title == fixture.Request.Title).ToListAsync());
    }

    [Fact]
    public async Task IndependentOwnersConvergeOnSameNativeIdentity() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var second = Owner(fixture.Services, Factory(fixture.Services));
        var results = await Task.WhenAll(
            fixture.Owner.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request),
            second.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request));
        Assert.Equal(results[0].Receipt, results[1].Receipt);
        Assert.Equal(results[0].Node.Id, results[1].Node.Id);
        Assert.Single(results, result => result.WasReplay);
        await using var database = await fixture.Factory.CreateDbContextAsync();
        Assert.Single(await database.Set<ProjectWorkflowContributionRecord>()
            .Where(row => row.RunId == fixture.Plan.Identity.Occurrence.RunId.Value).ToListAsync());
    }

    [Fact]
    public async Task SameIntentRejectsChangedContentAndTargetBeforeAnotherMutation() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var first = await fixture.Owner.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request);
        var changed = fixture.Request with { Notes = "Different prepared content" };
        var changedPlan = fixture.Plan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(fixture.Plan, changed) };
        await Assert.ThrowsAsync<WorkflowStructureOutputConflictException>(() => fixture.Owner.CreateWorkflowContributionAsync(changedPlan, changed));
        var parent = await fixture.Owner.CreateObjectAsync(fixture.ProjectId,
            new(ProjectObjectType.ProjectBlock, "Other parent", "", "", fixture.Request.ParentNodeKey));
        var movedRequest = fixture.Request with { ParentNodeKey = parent.Id };
        var movedPlan = fixture.Plan with {
            ParentNodeId = new(parent.Id),
            TargetBindingFingerprint = await fixture.Owner.ReadWorkflowTargetBindingAsync(fixture.ProjectId, parent.Id)
        };
        movedPlan = movedPlan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(movedPlan, movedRequest) };
        await Assert.ThrowsAsync<WorkflowStructureOutputConflictException>(() => fixture.Owner.CreateWorkflowContributionAsync(movedPlan, movedRequest));
        Assert.Equal(first.Receipt, (await fixture.Owner.FindWorkflowContributionAsync(fixture.Plan.Identity))!.Receipt);
    }

    [Fact]
    public async Task RestartReplayPreservesHumanEditsAndDeletionTombstone() {
        await using var environment = CanDoItAllTestEnvironment.Create("workflow-output-restart");
        var profile = environment.CreatePostgreSqlProfile("primary");
        Fixture fixture;
        ProjectWorkflowContributionResult first;
        await using (var app = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = RemoveAutomaticDelivery })) {
            fixture = await CreateFixtureAsync(app);
            first = await fixture.Owner.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request);
            var edited = await fixture.Owner.UpdateObjectAsync(fixture.ProjectId, first.Node.Id, "Human title", "Human subtitle", "Human notes");
            Assert.NotNull(edited);
            await fixture.DisposeAsync();
        }

        await using var restarted = await TestApplication.CreateAsync(new() { TestEnvironment = environment, ActiveProfile = profile, ConfigureServices = RemoveAutomaticDelivery });
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var owner = restartedScope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
        var replay = await owner.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request);
        Assert.Equal(first.Receipt, replay.Receipt);
        Assert.Equal(first.Node.Id, replay.Node.Id);
        var current = Assert.Single((await owner.GetStructureAsync(fixture.ProjectId)).Nodes, node => node.Id == first.Node.Id);
        Assert.Equal("Human title", current.Title);
        Assert.Equal("Human notes", current.Notes);
        Assert.Equal(1, await owner.DeleteObjectAsync(fixture.ProjectId, first.Node.Id));
        var tombstone = await owner.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request);
        Assert.True(tombstone.WasReplay);
        Assert.True(tombstone.TargetDeleted);
        Assert.Equal(first.Receipt, tombstone.Receipt);
        Assert.DoesNotContain((await owner.GetStructureAsync(fixture.ProjectId)).Nodes, node => node.Id == first.Node.Id);
    }

    [Fact]
    public async Task ManifestRecoversExactReceiptAfterRestartWithoutDiscoveringUnrelatedNodes() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        await SaveRunAsync(fixture.Services, fixture.Plan.Identity.Occurrence.RunId, fixture.Definition);
        var outputs = OutputStore(fixture.Services);
        await outputs.PrepareAsync(fixture.Plan);
        var applied = await fixture.Owner.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request);
        var unrelated = await fixture.Owner.CreateObjectAsync(fixture.ProjectId, fixture.Request with { Title = "Unrelated concurrent work" });

        var restarted = OutputStore(fixture.Services);
        Assert.Null((await restarted.FindAsync(fixture.Plan.Identity))!.Receipt);
        var receipt = (await Owner(fixture.Services, Factory(fixture.Services)).FindWorkflowContributionAsync(fixture.Plan.Identity))!.Receipt!;
        await restarted.CompleteAsync(receipt);
        var manifest = Assert.Single(await restarted.ListAsync(fixture.Plan.Identity.Occurrence.RunId));
        Assert.Equal(applied.Receipt, manifest.Receipt);
        Assert.NotEqual(unrelated.Id, manifest.Receipt!.NodeId);
        await restarted.CompleteAsync(receipt);
        Assert.Single(await restarted.ListAsync(fixture.Plan.Identity.Occurrence.RunId));
    }

    [Fact]
    public async Task LaterAdmissionWinsAndStatusQueriesDoNotProjectOrChangeReceipts() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var workflowNode = await fixture.Owner.CreateObjectAsync(fixture.ProjectId,
            new(ProjectObjectType.WorkflowDefinition, "Workflow", "", "", fixture.Request.ParentNodeKey,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() {
                    Workflow = new() { WorkflowId = fixture.Definition.Id, WorkflowVersionId = fixture.Definition.VersionId }
                })));
        var authority = await fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>().CaptureAsync(fixture.ProjectId,
            ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface));
        var actor = new ProjectStructureAgentContext("fixture", "Fixture", "fixture", "", "", "fixture-session") {
            WorkflowAuthority = ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface)
        };
        var first = await fixture.Owner.PrepareWorkflowAdmissionAsync(fixture.ProjectId, workflowNode.Id, Guid.NewGuid(), true,
            fixture.Definition, "{}", null, WorkflowPreviewSimulationPlan.Empty, authority, actor);
        var second = await fixture.Owner.PrepareWorkflowAdmissionAsync(fixture.ProjectId, workflowNode.Id, Guid.NewGuid(), true,
            fixture.Definition, "{}", null, WorkflowPreviewSimulationPlan.Empty, authority, actor);
        Assert.Equal(first.Binding.Sequence + 1, second.Binding.Sequence);
        var r1 = await SaveRunAsync(fixture.Services, first.Binding.RunId, fixture.Definition, first.LaunchIntent.Origin);
        var r2 = await SaveRunAsync(fixture.Services, second.Binding.RunId, fixture.Definition, second.LaunchIntent.Origin);
        var before = await ReadWorkflowNodeAsync(fixture.Factory, fixture.ProjectId, workflowNode.Id);
        var queries = fixture.Services.GetRequiredService<ProjectStructureWorkflowNodeService>();
        var status = await queries.GetStatusAsync(fixture.ProjectId, workflowNode.Id);
        Assert.Equal(r2.RunId, status.RunId);
        Assert.Equal(before, await ReadWorkflowNodeAsync(fixture.Factory, fixture.ProjectId, workflowNode.Id));
        Assert.Equal(ProjectWorkflowDeliveryState.Applied, await queries.ReconcileAsync(second.Binding.IntentId));
        Assert.Equal(ProjectWorkflowDeliveryState.Superseded,
            await fixture.Owner.DeliverWorkflowStatusAsync(first, Status(r1), r1, true));
        var native = await ReadWorkflowNodeAsync(fixture.Factory, fixture.ProjectId, workflowNode.Id);
        Assert.Equal(r2.RunId, ProjectObjectMetadataSerializer.Parse(native.MetadataJson).Workflow!.LastRunId);
        Assert.Equal(100, native.ProgressPercent);
        Assert.Equal(r2.RunId, (await queries.GetStatusAsync(fixture.ProjectId, workflowNode.Id)).RunId);
        Assert.Equal(ProjectWorkflowDeliveryState.Superseded, await queries.ReconcileAsync(first.Binding.IntentId));
        Assert.Equal(native, await ReadWorkflowNodeAsync(fixture.Factory, fixture.ProjectId, workflowNode.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AdmissionLostAcknowledgementRetainsExactIntentRunAndSequence(bool afterCommit) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var node = await fixture.Owner.CreateObjectAsync(fixture.ProjectId,
            new(ProjectObjectType.WorkflowDefinition, "Workflow", "", "", fixture.Request.ParentNodeKey,
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() {
                    Workflow = new() { WorkflowId = fixture.Definition.Id, WorkflowVersionId = fixture.Definition.VersionId }
                })));
        var authority = await fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>().CaptureAsync(fixture.ProjectId,
            ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface));
        var actor = new ProjectStructureAgentContext("fixture", "Fixture", "fixture", "", "", "admission-session") {
            WorkflowAuthority = ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface)
        };
        var intent = Guid.NewGuid();
        var fault = new CommitFault(afterCommit);
        var failing = Owner(fixture.Services, Factory(fixture.Services, new ObserveReceiptCommands(fault, "Workbench_WorkflowAdmissions"), fault));
        var thrown = await Assert.ThrowsAsync<ArgumentException>(() => failing.PrepareWorkflowAdmissionAsync(fixture.ProjectId, node.Id, intent, true,
            fixture.Definition, "{}", null, WorkflowPreviewSimulationPlan.Empty, authority, actor));
        Assert.Same(fault.Failure, thrown);
        Assert.True(fault.SawReceiptInsert);
        var saved = await fixture.Owner.FindWorkflowAdmissionAsync(intent);
        Assert.Equal(afterCommit, saved is not null);
        var independent = Owner(fixture.Services, Factory(fixture.Services));
        var replay = await independent.PrepareWorkflowAdmissionAsync(fixture.ProjectId, node.Id, intent, true,
            fixture.Definition, "{}", null, WorkflowPreviewSimulationPlan.Empty, authority, actor);
        Assert.Equal(1, replay.Binding.Sequence);
        if (saved is not null) {
            Assert.Equal(saved.Binding.RunId, replay.Binding.RunId);
            Assert.Equal(saved.LaunchIntent.Idempotency, replay.LaunchIntent.Idempotency);
        }

        var deliberateRepeat = await independent.PrepareWorkflowAdmissionAsync(fixture.ProjectId, node.Id, Guid.NewGuid(), false,
            fixture.Definition, "{}", null, WorkflowPreviewSimulationPlan.Empty, authority, actor);
        Assert.Equal(2, deliberateRepeat.Binding.Sequence);
        Assert.NotEqual(replay.Binding.RunId, deliberateRepeat.Binding.RunId);
        Assert.False(deliberateRepeat.CallerSuppliedIntent);
        await using var database = await fixture.Factory.CreateDbContextAsync();
        Assert.Equal(2, await database.Set<ProjectWorkflowAdmissionRecord>().CountAsync(row => row.ProjectId == fixture.ProjectId));
    }

    [Fact]
    public async Task AssetDispatchClaimSurvivesRestartAndCannotAdmitAnotherPlacement() {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        await SaveRunAsync(fixture.Services, fixture.Plan.Identity.Occurrence.RunId, fixture.Definition);
        var plan = fixture.Plan with { Kind = WorkflowStructureOutputKind.Asset };
        var outputs = OutputStore(fixture.Services);
        var prepared = await outputs.PrepareAsync(plan);
        Assert.NotNull(prepared.StoragePlacementIntentId);
        Assert.True(await outputs.TryBeginAssetDispatchAsync(plan.Identity));
        var restarted = OutputStore(fixture.Services);
        Assert.False(await restarted.TryBeginAssetDispatchAsync(plan.Identity));
        Assert.Equal(prepared.StoragePlacementIntentId, (await restarted.FindAsync(plan.Identity))!.StoragePlacementIntentId);
        Assert.Equal(WorkflowStructureOutputState.Prepared, (await restarted.FindAsync(plan.Identity))!.State);
        Assert.Null((await restarted.FindAsync(plan.Identity))!.Receipt);
        await restarted.DeferInspectionAsync(plan.Identity);
        Assert.DoesNotContain(await restarted.ListPendingAsync(128), output => output.Plan.Identity == plan.Identity);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AssetPlacementReceiptSurvivesNativeRollbackOrLostAcknowledgementWithoutAnotherObject(bool afterCommit) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var request = fixture.Request with {
            ObjectType = ProjectObjectType.File,
            ObjectSubtype = "json",
            Media = new("proof.json", "application/json", Convert.ToBase64String("{\"enabled\":true}"u8.ToArray()))
        };
        var plan = fixture.Plan with { Kind = WorkflowStructureOutputKind.Asset };
        plan = plan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(plan, request) };
        await SaveRunAsync(fixture.Services, plan.Identity.Occurrence.RunId, fixture.Definition);
        var outputs = OutputStore(fixture.Services);
        var prepared = await outputs.PrepareAsync(plan);
        Assert.NotNull(prepared.StoragePlacementIntentId);
        var fault = new CommitFault(afterCommit);
        var failing = Owner(fixture.Services, Factory(fixture.Services, new ObserveReceiptCommands(fault), fault));
        var thrown = await Assert.ThrowsAsync<ArgumentException>(() => failing.CreateWorkflowContributionAsync(plan, request,
            storagePlacementIntentId: prepared.StoragePlacementIntentId));
        Assert.Same(fault.Failure, thrown);
        Assert.True(fault.SawReceiptInsert);
        var storageOwner = fixture.Services.GetRequiredService<StorageStablePlacementService>();
        var placement = await storageOwner.FindAsync(new(prepared.StoragePlacementIntentId.Value));
        Assert.NotNull(placement);
        Assert.Equal(StorageStablePlacementState.Completed, placement.State);
        Assert.NotNull(placement.Receipt);
        Assert.True(File.Exists(placement.Receipt.Location));
        Assert.Equal("{\"enabled\":true}", await File.ReadAllTextAsync(placement.Receipt.Location));
        var filesBefore = Directory.GetFiles(Path.GetDirectoryName(placement.Receipt.Location)!).Order().ToArray();
        var restarted = Owner(fixture.Services, Factory(fixture.Services));
        var applied = await restarted.CreateWorkflowContributionAsync(plan, request,
            storagePlacementIntentId: prepared.StoragePlacementIntentId);
        Assert.Equal(afterCommit, applied.WasReplay);
        Assert.Equal(prepared.StoragePlacementIntentId, applied.Receipt!.AssetId);
        Assert.Equal(placement.Receipt, (await storageOwner.FindAsync(new(prepared.StoragePlacementIntentId.Value)))!.Receipt);
        Assert.Equal(filesBefore, Directory.GetFiles(Path.GetDirectoryName(placement.Receipt.Location)!).Order().ToArray());
        await outputs.CompleteAsync(applied.Receipt);
        Assert.Equal(applied.Receipt, (await outputs.FindAsync(plan.Identity))!.Receipt);
        Assert.Equal(applied.Receipt, (await restarted.CreateWorkflowContributionAsync(plan, request,
            storagePlacementIntentId: prepared.StoragePlacementIntentId)).Receipt);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReconcilerResumesExactAdmittedNativeCommandAfterRunFailureButHonorsCancellation(bool cancelled) {
        await using var app = await TestApplication.CreateAsync(new() { ConfigureServices = RemoveAutomaticDelivery });
        await using var fixture = await CreateFixtureAsync(app);
        var authority = await fixture.Services.GetRequiredService<ProjectStructureWorkflowAuthorityService>().CaptureAsync(fixture.ProjectId,
            ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface));
        var origin = new WorkflowLaunchOrigin.Preview(authority.Principal, new("prepared-output-recovery")) { StructureAuthority = authority };
        var saved = await SaveRunAsync(fixture.Services, fixture.Plan.Identity.Occurrence.RunId, fixture.Definition, origin);
        var outputs = OutputStore(fixture.Services);
        await outputs.PrepareAsync(fixture.Plan);
        var fault = new CommitFault(afterCommit: false);
        var failing = Owner(fixture.Services, Factory(fixture.Services, new ObserveReceiptCommands(fault), fault));
        Assert.Same(fault.Failure, await Assert.ThrowsAsync<ArgumentException>(() => failing.CreateWorkflowContributionAsync(fixture.Plan, fixture.Request)));
        Assert.Null(await fixture.Owner.FindWorkflowContributionAsync(fixture.Plan.Identity));
        Assert.NotNull(await fixture.Owner.FindPreparedWorkflowContributionAsync(fixture.Plan.Identity));
        await fixture.Services.GetRequiredService<IWorkflowRunStore>().SaveRunAsync(saved with {
            State = cancelled ? WorkflowRunState.Cancelled : WorkflowRunState.Failed
        });
        var gateway = Assert.IsType<WorkbenchProjectStructureRuntimeGateway>(fixture.Services.GetRequiredService<IProjectStructureRuntimeGateway>());
        if (cancelled) {
            var error = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => gateway.ReconcileWorkflowOutputAsync(fixture.Plan.Identity));
            Assert.Equal(409, error.StatusCode);
            Assert.Null(await fixture.Owner.FindWorkflowContributionAsync(fixture.Plan.Identity));
            Assert.Null((await outputs.FindAsync(fixture.Plan.Identity))!.Receipt);
        } else {
            Assert.True(await gateway.ReconcileWorkflowOutputAsync(fixture.Plan.Identity));
            var receipt = (await fixture.Owner.FindWorkflowContributionAsync(fixture.Plan.Identity))!.Receipt!;
            Assert.Equal(fixture.Plan.Identity, receipt.Identity);
            Assert.Equal(fixture.Plan.Fingerprint, receipt.Fingerprint);
            Assert.Equal(receipt, (await outputs.FindAsync(fixture.Plan.Identity))!.Receipt);
            Assert.True(await gateway.ReconcileWorkflowOutputAsync(fixture.Plan.Identity));
            Assert.Equal(receipt, (await outputs.FindAsync(fixture.Plan.Identity))!.Receipt);
        }
        await using var database = await fixture.Factory.CreateDbContextAsync();
        Assert.Equal(cancelled ? 0 : 1, await database.Set<ProjectObjectRecord>().CountAsync(row =>
            row.ProjectId == fixture.ProjectId && row.Title == fixture.Request.Title));
    }

    private static void RemoveAutomaticDelivery(IServiceCollection services) {
        foreach (var descriptor in services.Where(descriptor => descriptor.ImplementationType == typeof(ProjectStructureWorkflowDeliveryWorker)).ToArray()) {
            services.Remove(descriptor);
        }
    }

    private static async Task<Fixture> CreateFixtureAsync(TestApplication app) {
        var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projects = services.GetRequiredService<ProjectsService>();
        var saved = await projects.SaveAsync(new ProjectEditorModel { Name = "Workflow delivery fixture", CurrentPhase = "Execution" });
        Assert.True(saved.IsSuccess);
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var definition = Definition();
        var parent = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(saved.Value);
        var request = new ProjectObjectCreateRequest(ProjectObjectType.WorkItem, $"Contribution {Guid.NewGuid():N}", "", "Initial notes", parent,
            ObjectSubtype: "task", MetadataJson: "{}", PlacementIntent: ProjectObjectPlacementIntent.AutomaticAroundParent);
        var plan = new WorkflowStructureOutputPlan(new(WorkflowExecutionOccurrence.Start(WorkflowRunId.New())
                .Advance(definition.VersionId, new("effect")), 0), definition.VersionId, new("effect"), saved.Value,
            new(parent), await owner.ReadWorkflowTargetBindingAsync(saved.Value, parent), WorkflowStructureOutputKind.Task,
            WorkflowStructureOutputRole.RequiredResult, string.Empty);
        plan = plan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(plan, request) };
        return new(saved.Value, owner, Factory(services), definition, plan, request, scope);
    }

    private static PersistentWorkflowStructureOutputStore OutputStore(IServiceProvider services) =>
        new(services.GetRequiredService<IDbContextFactory<WorkflowDbContext>>(), TimeProvider.System);

    private static OwnerFactory Factory(IServiceProvider services, params IInterceptor[] interceptors) {
        var options = new DbContextOptionsBuilder<WorkbenchDbContext>();
        AppDbContextOptionsConfigurator.Configure(options, services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile);
        options.AddInterceptors(interceptors);
        return new(options.Options);
    }

    private static ProjectWorkbenchService Owner(IServiceProvider services, IDbContextFactory<WorkbenchDbContext> factory)
        => new(factory, services.GetRequiredService<ProjectStructureMutationScopeFactory>(),
            services.GetRequiredService<ProjectRecordQueryService>(), services.GetRequiredService<IClock>(), services.GetRequiredService<ProjectAssetStorageService>(),
            services.GetRequiredService<ProjectStructureAssemblyService>(),
            services.GetRequiredService<ProjectWorkbenchRelationService>(), services.GetRequiredService<ProjectWorkbenchLifecycleService>(),
            services.GetRequiredService<ProjectWorkbenchCommandService>(), services.GetRequiredService<ProjectWorkbenchCrossModuleMutationService>(),
            services.GetRequiredService<ProjectStructureRuntimeNodeMetadataBoundary>());

    private static async Task<WorkflowRunSnapshot> SaveRunAsync(IServiceProvider services, WorkflowRunId id,
        WorkflowDefinition definition, WorkflowLaunchOrigin? origin = null) {
        var now = DateTimeOffset.UtcNow;
        var run = new WorkflowRunSnapshot(id, definition.Id, definition.VersionId, WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess, id.ToString(), "Completed", now, now) { Origin = origin };
        await services.GetRequiredService<IWorkflowRunStore>().SaveRunAsync(run);
        return run;
    }

    private static ProjectStructureWorkflowRunStatus Status(WorkflowRunSnapshot run)
        => new(run.RunId, run.State, "Completed", "progress", 100, "", "", "", 2, 2, run.Summary,
            new(run.RunId, run.State, "Fixture", run.Summary, 2, 2, [], [], [], []), []);

    private static async Task<NodeObservation> ReadWorkflowNodeAsync(OwnerFactory factory, Guid projectId, string nodeId) {
        await using var database = await factory.CreateDbContextAsync();
        return await database.Set<ProjectObjectRecord>().AsNoTracking()
            .Where(node => node.ProjectId == projectId && node.NodeKey == nodeId)
            .Select(node => new NodeObservation(node.MetadataJson, node.ProgressPercent, node.Status, node.MarkersJson, node.UpdatedAtUtc)).SingleAsync();
    }

    private static WorkflowDefinition Definition() {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        WorkflowNode Node(WorkflowNodeId id, WorkflowNodeKind kind) => new(id, kind, id.Value, [],
            new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text));
        var now = DateTimeOffset.UtcNow;
        return new(WorkflowId.New(), WorkflowVersionId.New(), "Fixture", "Fixture", WorkflowLifecycleStatus.Active,
            new(start, [Node(start, WorkflowNodeKind.Start), Node(end, WorkflowNodeKind.End)],
                [new(new("start-end"), start, null, end, null, WorkflowEdgeKind.Direct, string.Empty)]),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), now, now);
    }

    private sealed record Fixture(Guid ProjectId, ProjectWorkbenchService Owner, OwnerFactory Factory,
        WorkflowDefinition Definition, WorkflowStructureOutputPlan Plan, ProjectObjectCreateRequest Request, AsyncServiceScope Scope) : IAsyncDisposable {
        public IServiceProvider Services => Scope.ServiceProvider;
        public ValueTask DisposeAsync() => Scope.DisposeAsync();
    }
    private sealed record NodeObservation(string MetadataJson, int ProgressPercent, string Status, string MarkersJson, DateTimeOffset UpdatedAtUtc);
    private sealed class OwnerFactory(DbContextOptions<WorkbenchDbContext> options) : IDbContextFactory<WorkbenchDbContext> {
        public WorkbenchDbContext CreateDbContext() => new(options);
        public Task<WorkbenchDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(CreateDbContext());
    }

    private sealed class CommitFault(bool afterCommit) : DbTransactionInterceptor {
        public ArgumentException Failure { get; } = new("Injected native receipt acknowledgement failure.");
        public bool SawReceiptInsert { get; set; }
        private bool thrown;
        public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction,
            TransactionEventData eventData, InterceptionResult result, CancellationToken cancellationToken = default) {
            if (!afterCommit && SawReceiptInsert && !thrown) {
                thrown = true;
                throw Failure;
            }

            return ValueTask.FromResult(result);
        }

        public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData,
            CancellationToken cancellationToken = default) {
            if (afterCommit && SawReceiptInsert && !thrown) {
                thrown = true;
                throw Failure;
            }

            return Task.CompletedTask;
        }
    }

    private sealed class ObserveReceiptCommands(CommitFault fault, string table = "Workbench_WorkflowContributionReceipts") : DbCommandInterceptor {
        private bool sawNativeInsert;
        private bool sawReceiptUpdate;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (table == "Workbench_WorkflowContributionReceipts") {
                sawNativeInsert |= command.CommandText.Contains("INSERT INTO \"Workbench_ProjectObjects\"", StringComparison.Ordinal);
                sawReceiptUpdate |= command.CommandText.Contains("UPDATE \"Workbench_WorkflowContributionReceipts\"", StringComparison.Ordinal) &&
                    command.CommandText.Contains("\"ReceiptJson\"", StringComparison.Ordinal);
                fault.SawReceiptInsert = sawNativeInsert && sawReceiptUpdate;
            } else if (command.CommandText.Contains($"INSERT INTO \"{table}\"", StringComparison.Ordinal)) {
                fault.SawReceiptInsert = true;
            }

            return ValueTask.FromResult(result);
        }
    }
}
