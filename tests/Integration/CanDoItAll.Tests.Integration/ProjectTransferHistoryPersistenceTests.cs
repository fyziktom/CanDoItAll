using System.IO.Compression;
using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Drivers.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed partial class ProjectTransferHistoryPersistenceTests {
    private static readonly DateTimeOffset Now = new(2026, 4, 5, 6, 7, 8, TimeSpan.Zero);
    private static readonly JsonSerializerOptions PackageJson = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter() } };

    [Fact]
    public async Task Row_transfer_preserves_all_retained_states_with_fresh_live_lifetimes_and_survives_restart() {
        await using var fixture = await TransferFixture.CreateAsync();
        var original = await SeedEvidenceAsync(fixture, includeProject: true);
        var evidence = EvidenceSnapshot(original);
        var sourceLifetime = original.Projects.Single().LifetimeId;
        var sourceProbe = new SourceSnapshotProbe();
        var result = await fixture.TransferAsync(sourceProbe: sourceProbe);
        Assert.True(result.Success, result.Message);
        Assert.NotEmpty(sourceProbe.ReadTransactions);
        Assert.All(sourceProbe.ReadTransactions, isolation => Assert.Equal(IsolationLevel.Serializable, isolation));
        Assert.Equal(original.Counts.Total, result.RecordsCopied);
        await fixture.RestartTargetAsync();
        await using var target = await fixture.TargetContextAsync();
        var restored = await fixture.LoadAsync(target: true);
        Assert.Equal(evidence, EvidenceSnapshot(restored));
        Assert.NotEqual(sourceLifetime, restored.Projects.Single().LifetimeId);
        Assert.False(restored.Projects.Single().LegacyAgentAccessBindingEligible);
        Assert.All(Provenance(restored), value => Assert.Equal(fixture.SourceProfile.Profile.Id, Assert.IsType<RetainedEvidenceImport>(value).SourceProfileId));
        Assert.Equal(original.Retirements.Single().ImportedHistory, restored.Retirements.Single().ImportedHistory!.Previous);
        await using var source = await fixture.SourceContextAsync();
        var unchanged = await fixture.LoadAsync(target: false);
        Assert.Equal(evidence, EvidenceSnapshot(unchanged));
        Assert.Equal(sourceLifetime, unchanged.Projects.Single().LifetimeId);
        Assert.Null(unchanged.CreationReservations[0].ImportedHistory);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ClearTargetAsync());

        await using var projects = await fixture.TargetServices.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        await using var workbench = await fixture.TargetServices.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
        Assert.Equal(6, projects.Model.GetEntityTypes().Count());
        Assert.Equal(15, workbench.Model.GetEntityTypes().Count());
        foreach (var type in new[] { typeof(ProjectRetirementRecord), typeof(ProjectCreationReservationRecord), typeof(ProjectWorkflowContributionRecord), typeof(ProjectWorkflowAdmissionRecord) }) {
            var model = type == typeof(ProjectRetirementRecord) || type == typeof(ProjectCreationReservationRecord) ? projects.Model : workbench.Model;
            Assert.Equal("TEXT", model.FindEntityType(type)!.FindProperty(nameof(ProjectRetirementRecord.ImportedHistory))!.GetColumnType());
            var owner = type == typeof(ProjectRetirementRecord) || type == typeof(ProjectCreationReservationRecord)
                ? projects.GetService<IDesignTimeModel>().Model : workbench.GetService<IDesignTimeModel>().Model;
            Assert.Equal(target.GetService<IDesignTimeModel>().Model.FindEntityType(type)!.ToDebugString(MetadataDebugStringOptions.LongDefault),
                owner.FindEntityType(type)!.ToDebugString(MetadataDebugStringOptions.LongDefault));
        }
        await using var transaction = await projects.Database.BeginTransactionAsync();
        using var coordination = fixture.TargetServices.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(projects);
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => fixture.TargetServices.GetRequiredService<ProjectWriteAdmissionService>()
            .RequireForMutationAsync(new(fixture.TargetProfile.Profile.Id, restored.Projects.Single().Id, sourceLifetime)));
        await transaction.RollbackAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Project_import_preserves_an_unrelated_empty_organization_cursor(bool package) {
        await using var fixture = await TransferFixture.CreateAsync();
        var project = new Project { Name = "Project-only import", CurrentPhase = "Execution" };
        await using (var source = await fixture.SourceContextAsync()) {
            source.Add(project);
            await source.SaveChangesAsync();
        }
        var cursor = new AiTechnicalProjectionCursor {
            DatabaseProfileId = fixture.TargetProfile.Profile.Id,
            SourceScopeKind = WorkspaceScopeKind.Organization,
            SourceScopeKey = fixture.TargetProfile.Profile.Id.ToString("N"),
            CatalogRevision = 17, ProjectionSha256 = new string('A', 64), UpdatedAtUtc = Now
        };
        var original = JsonSerializer.Serialize(cursor);
        await using (var target = await fixture.TargetContextAsync()) {
            target.Add(cursor);
            await target.SaveChangesAsync();
            Assert.Empty(await target.Set<AiResourceBinding>().ToArrayAsync());
        }
        if (package) {
            var service = fixture.PackageService();
            var exported = await service.ExportAllAsync(new() { PackagePath = Path.Combine(fixture.Environment.RootPath, "cursor.cdaproj") });
            Assert.True(exported.IsSuccess, string.Join("; ", exported.Errors.Select(error => error.Message)));
            var imported = await service.ImportAllAsync(new() { PackagePath = exported.Value!.PackagePath, TargetProfileId = fixture.TargetProfile.Profile.Id });
            Assert.True(imported.IsSuccess, string.Join("; ", imported.Errors.Select(error => error.Message)));
        } else {
            var imported = await fixture.TransferAsync();
            Assert.True(imported.Success, imported.Message);
        }
        await fixture.RestartTargetAsync();
        await using var restarted = await fixture.TargetContextAsync();
        Assert.Equal(project.Id, (await restarted.Set<Project>().SingleAsync()).Id);
        Assert.Equal(original, JsonSerializer.Serialize(await restarted.Set<AiTechnicalProjectionCursor>().AsNoTracking().SingleAsync()));
        Assert.Empty(await restarted.Set<AiResourceBinding>().ToArrayAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Project_import_preserves_unrelated_local_launches_and_terminal_runtime_admission_pointers(bool package) {
        await using var fixture = await TransferFixture.CreateAsync();
        var project = new Project { Name = "Local launches survive project import", CurrentPhase = "Execution" };
        await using (var source = await fixture.SourceContextAsync()) {
            source.Add(project);
            await source.SaveChangesAsync();
        }
        var options = new DbContextOptionsBuilder<ProcessPersistenceDbContext>().UseNpgsql(fixture.TargetProfile.ConnectionString).Options;
        var store = new EfProcessPreparedLaunchStore(new ProcessFactory(options), options);
        var authority = await fixture.TargetServices.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureLocalAsync(null, ProcessLaunchOperatorSurface.UserInterface);
        foreach (var state in Enum.GetValues<ProcessLaunchContinuationState>()) {
            foreach (var status in new[] { ProcessRuntimeStatus.Completed, ProcessRuntimeStatus.Failed, ProcessRuntimeStatus.Cancelled }) {
                var preparation = ProcessPreparedLaunchFixture.Create(authority, new(Guid.NewGuid()));
                var initial = preparation.InitialCommit;
                var initialState = initial.Mutation.State with {
                    Steps = initial.Mutation.State.Steps.Select(step => step with { Status = ProcessRuntimeStepStatus.Pending }).ToArray()
                };
                preparation = preparation with {
                    InitialCommit = initial with { OriginalState = initialState, Mutation = initial.Mutation with { State = initialState } }
                };
                var saved = await store.PrepareAsync(preparation);
                await using var target = new ProcessPersistenceDbContext(options);
                Assert.True(target.Database.IsNpgsql());
                var unitOfWork = new EfProcessRuntimeUnitOfWork(target,
                    coordinatedTransaction: CoordinatedDatabaseTransaction.ForProfile(fixture.TargetProfile),
                    launchAuthorityPolicy: fixture.TargetServices.GetRequiredService<IProcessLaunchAuthorityPolicy>());
                var accepted = await unitOfWork.CommitAsync(ProcessPreparedLaunchFixture.Commit(saved));
                Assert.True(accepted.Succeeded);
                await CompleteRetainedProcessAsync(unitOfWork, saved, accepted.State, status);
                var launch = await target.PreparedLaunches.SingleAsync(item => item.Id == saved.Preparation.AdmissionId.Value);
                var runtime = await target.RuntimeStates.SingleAsync(item => item.RunId == launch.RunId);
                Assert.NotNull(launch.AcceptedAtUtc);
                Assert.Equal(launch.Id, runtime.LaunchAdmissionId);
                Assert.Equal(status, runtime.Status);
                Assert.False(launch.ReferencesProject());
                launch.State = state;
                await target.SaveChangesAsync();
            }
        }
        string originalLaunches;
        string originalStates;
        await using (var target = await fixture.TargetContextAsync()) {
            originalLaunches = JsonSerializer.Serialize(await target.Set<ProcessPreparedLaunchEntity>().AsNoTracking().OrderBy(item => item.Id).ToArrayAsync());
            originalStates = JsonSerializer.Serialize(await target.Set<ProcessRuntimeStateEntity>().AsNoTracking().OrderBy(item => item.RunId).ToArrayAsync());
        }
        if (package) {
            var service = fixture.PackageService();
            var exported = await service.ExportAllAsync(new() { PackagePath = Path.Combine(fixture.Environment.RootPath, "local-process.cdaproj") });
            Assert.True(exported.IsSuccess, string.Join("; ", exported.Errors.Select(error => error.Message)));
            var imported = await service.ImportAllAsync(new() { PackagePath = exported.Value!.PackagePath, TargetProfileId = fixture.TargetProfile.Profile.Id });
            Assert.True(imported.IsSuccess, string.Join("; ", imported.Errors.Select(error => error.Message)));
        } else {
            var imported = await fixture.TransferAsync();
            Assert.True(imported.Success, imported.Message);
        }
        await fixture.RestartTargetAsync();
        await using var restarted = await fixture.TargetContextAsync();
        Assert.Equal(project.Id, (await restarted.Set<Project>().SingleAsync()).Id);
        Assert.Equal(originalLaunches, JsonSerializer.Serialize(await restarted.Set<ProcessPreparedLaunchEntity>().AsNoTracking().OrderBy(item => item.Id).ToArrayAsync()));
        Assert.Equal(originalStates, JsonSerializer.Serialize(await restarted.Set<ProcessRuntimeStateEntity>().AsNoTracking().OrderBy(item => item.RunId).ToArrayAsync()));
    }

    private static async Task CompleteRetainedProcessAsync(EfProcessRuntimeUnitOfWork unitOfWork,
        ProcessPreparedLaunchSnapshot launch, ProcessRuntimeStateSnapshot state, ProcessRuntimeStatus terminalStatus) {
        var engine = new ProcessRuntimeEngine(unitOfWork);
        var time = state.UpdatedAtUtc;
        RuntimeCommandContext Command() {
            time = time.AddSeconds(1);
            return new(RuntimeCommandId.New(), new(ProcessEventActorKind.System, new("retained-history-fixture")),
                new("retained-history-fixture"), time);
        }
        if (terminalStatus == ProcessRuntimeStatus.Cancelled) {
            var cancelled = await engine.RequestCancellationAsync(state, Command());
            Assert.True(cancelled.Succeeded);
            Assert.Equal(terminalStatus, cancelled.State.Status);
            return;
        }
        var active = await engine.ActivateAsync(state, Command());
        Assert.True(active.Succeeded);
        var ready = await engine.ScheduleReadyAsync(active.State, Command());
        Assert.True(ready.Succeeded);
        var work = Assert.Single(new ProcessRuntimeScheduler().CalculateReadyWork(ready.State, launch.Preparation.InitialCommit.InitialPlan!, time));
        var owner = new DispatcherOwnerId("retained-history-fixture");
        var token = DispatchClaimToken.New();
        var claimed = await engine.CreateClaimAsync(ready.State, Command(), new(work, owner, token, time.AddMinutes(5)));
        Assert.True(claimed.Succeeded);
        var running = await engine.MarkClaimRunningAsync(claimed.State, Command(), work.StepInstanceId, token);
        Assert.True(running.Succeeded);
        var result = new StrategyResultEnvelope(work.StrategyBinding.StrategyId, work.StrategyBinding.StrategyVersion,
            Guid.NewGuid(), terminalStatus == ProcessRuntimeStatus.Completed ? StrategyOutcome.Succeeded : StrategyOutcome.Failed,
            [], [], [], [], "sha256:" + new string('a', 64));
        var completed = await engine.SubmitStrategyResultAsync(running.State, Command(),
            new(work.StepInstanceId, owner, token, StrategyResultIdempotencyKey.New(), result));
        Assert.True(completed.Succeeded);
        Assert.Equal(terminalStatus, completed.State.Status);
        Assert.Equal(launch.Preparation.AdmissionId, completed.State.LaunchAdmissionId);
    }

    private sealed class ProcessFactory(DbContextOptions<ProcessPersistenceDbContext> options) : IDbContextFactory<ProcessPersistenceDbContext> {
        public ProcessPersistenceDbContext CreateDbContext() => new(options);
    }

    [Fact]
    public async Task A_late_history_insert_failure_rolls_back_projects_and_every_earlier_history_table() {
        await using var fixture = await TransferFixture.CreateAsync();
        var original = await SeedEvidenceAsync(fixture, includeProject: true);
        var fault = new RejectAdmissionInsert();
        await Assert.ThrowsAsync<InjectedTransferFailure>(() => fixture.TransferAsync(fault));
        Assert.True(fault.Observed);
        await using var target = await fixture.TargetContextAsync();
        Assert.Equal(0, (await fixture.LoadAsync(target: true)).Counts.Total);
        await using var source = await fixture.SourceContextAsync();
        Assert.Equal(EvidenceSnapshot(original), EvidenceSnapshot(await fixture.LoadAsync(target: false)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Project_packages_accept_v2_and_v3_without_reusing_source_authority(bool legacy) {
        await using var fixture = await TransferFixture.CreateAsync();
        var original = await SeedEvidenceAsync(fixture, includeProject: true);
        var service = fixture.PackageService();
        var exported = await service.ExportAllAsync(new() { PackagePath = Path.Combine(fixture.Environment.RootPath, "original.cdaproj") });
        Assert.True(exported.IsSuccess);
        Assert.Equal(ProjectPackageManifest.CurrentFormat, exported.Value!.Manifest.Format);
        Assert.Equal(ProjectPackageHistoryDisposition.PreserveAsHistory, exported.Value.Manifest.HistoryDisposition);
        var package = exported.Value.PackagePath;
        if (legacy) {
            package = await MakeLegacyPackageAsync(package, fixture.Environment.RootPath);
        }
        var imported = await service.ImportAllAsync(new() { PackagePath = package, TargetProfileId = fixture.TargetProfile.Profile.Id });
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Errors.Select(error => error.Message)));
        await using var target = await fixture.TargetContextAsync();
        var saved = await fixture.LoadAsync(target: true);
        Assert.Equal(original.Projects.Single().Id, saved.Projects.Single().Id);
        Assert.NotEqual(original.Projects.Single().LifetimeId, saved.Projects.Single().LifetimeId);
        Assert.False(saved.Projects.Single().LegacyAgentAccessBindingEligible);
        if (legacy) {
            Assert.Empty(Provenance(saved));
            Assert.Equal(ProjectPackageManifest.LegacyFormat, imported.Value!.Manifest.Format);
        } else {
            Assert.Equal(EvidenceSnapshot(original), EvidenceSnapshot(saved));
            Assert.All(Provenance(saved), value => Assert.Equal(exported.Value.Manifest.PackageId, value!.TransferId));
        }
        await using var source = await fixture.SourceContextAsync();
        Assert.Equal(EvidenceSnapshot(original), EvidenceSnapshot(await fixture.LoadAsync(target: false)));
    }

    [Fact]
    public async Task Existing_v3_packages_without_the_optional_Work_section_remain_importable() {
        await using var fixture = await TransferFixture.CreateAsync();
        var original = await SeedEvidenceAsync(fixture, includeProject: true);
        var service = fixture.PackageService();
        var exported = await service.ExportAllAsync(new() { PackagePath = Path.Combine(fixture.Environment.RootPath, "original-v3.cdaproj") });
        Assert.True(exported.IsSuccess);
        var compatible = await MakeLegacyPackageAsync(exported.Value!.PackagePath, fixture.Environment.RootPath, legacyFormat: false);
        var imported = await service.ImportAllAsync(new() { PackagePath = compatible, TargetProfileId = fixture.TargetProfile.Profile.Id });
        Assert.True(imported.IsSuccess, string.Join("; ", imported.Errors.Select(error => error.Message)));
        Assert.Null(imported.Value!.Manifest.WorkAssignmentHistoryVersion);
        Assert.Null(imported.Value.Manifest.ProcessAssetHistoryVersion);
        var restored = await fixture.LoadAsync(target: true);
        Assert.Empty(restored.WorkAssignmentHistory);
        Assert.Equal(EvidenceSnapshot(original), EvidenceSnapshot(restored));
    }

    [Fact]
    public async Task History_only_packages_preserve_deleted_project_evidence() {
        await using var fixture = await TransferFixture.CreateAsync();
        var original = await SeedEvidenceAsync(fixture, includeProject: false);
        var service = fixture.PackageService();
        var exported = await service.ExportAllAsync(new() { PackagePath = Path.Combine(fixture.Environment.RootPath, "history.cdaproj") });
        Assert.True(exported.IsSuccess);
        var imported = await service.ImportAllAsync(new() { PackagePath = exported.Value!.PackagePath, TargetProfileId = fixture.TargetProfile.Profile.Id });
        Assert.True(imported.IsSuccess);
        await using var target = await fixture.TargetContextAsync();
        var saved = await fixture.LoadAsync(target: true);
        Assert.Empty(saved.Projects);
        Assert.Equal(EvidenceSnapshot(original), EvidenceSnapshot(saved));
        Assert.Equal(original.Counts.Total, imported.Value!.RecordsImported);
    }

    [Fact]
    public async Task Imported_reservations_and_workflow_history_cannot_replay_but_fresh_target_admissions_work() {
        await using var fixture = await TransferFixture.CreateAsync();
        var sourceProjects = fixture.SourceServices.GetRequiredService<ProjectsService>();
        var saved = await sourceProjects.SaveAsync(new() { Name = "Source history", CurrentPhase = "Execution" });
        Assert.True(saved.IsSuccess);
        var projectId = saved.Value;
        var sourceOwner = fixture.SourceServices.GetRequiredService<ProjectWorkbenchService>();
        var parent = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(projectId);
        var definition = Definition();
        var request = new ProjectObjectCreateRequest(ProjectObjectType.WorkItem, "Original task", "", "Preserved notes", parent,
            ObjectSubtype: "task", MetadataJson: "{}");
        var sourceAuthority = await fixture.SourceServices.GetRequiredService<ProjectStructureWorkflowAuthorityService>().CaptureAsync(projectId,
            ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface));
        var prepared = await PrepareWorkflowTaskContributionAsync(fixture.SourceServices, definition, projectId, request, sourceAuthority);
        var plan = prepared.Plan;
        request = prepared.Request;
        var created = await sourceOwner.CreateWorkflowContributionAsync(plan, request);
        var workflowNode = await sourceOwner.CreateObjectAsync(projectId, new(ProjectObjectType.WorkflowDefinition, "Workflow", "", "", parent,
            MetadataJson: ProjectObjectMetadataSerializer.Serialize(new() { Workflow = new() { WorkflowId = definition.Id, WorkflowVersionId = definition.VersionId } })));
        var actor = new ProjectStructureAgentContext("history", "History", "fixture", "", "", "history-session") {
            WorkflowAuthority = ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface)
        };
        var admission = await sourceOwner.PrepareWorkflowAdmissionAsync(projectId, workflowNode.Id, Guid.NewGuid(), true,
            definition, "{}", null, WorkflowPreviewSimulationPlan.Empty, sourceAuthority, actor);
        var reserved = await fixture.SourceServices.GetRequiredService<ProjectWriteAdmissionService>()
            .ReserveCreationAsync(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        Assert.True((await fixture.TransferAsync()).Success);

        var targetOwner = fixture.TargetServices.GetRequiredService<ProjectWorkbenchService>();
        var history = await targetOwner.FindWorkflowContributionAsync(plan.Identity);
        Assert.NotNull(history!.ImportedHistory);
        Assert.Equal(created.Receipt, history.Receipt);
        await Assert.ThrowsAsync<InvalidOperationException>(() => targetOwner.CreateWorkflowContributionAsync(plan, request));
        await Assert.ThrowsAsync<InvalidOperationException>(() => targetOwner.FindPreparedWorkflowContributionAsync(plan.Identity));
        var historicalAdmission = await targetOwner.FindWorkflowAdmissionAsync(admission.Binding.IntentId);
        Assert.NotNull(historicalAdmission!.ImportedHistory);
        Assert.Equal(admission.Binding.IntentId, historicalAdmission.Binding.IntentId);
        Assert.Equal(admission.Binding.RunId, historicalAdmission.Binding.RunId);
        Assert.Equal(sourceAuthority.DatabaseProfileId, historicalAdmission.Binding.Authority.DatabaseProfileId);
        Assert.Empty(await targetOwner.ListWorkflowAdmissionsForDeliveryAsync(1));
        await Assert.ThrowsAsync<InvalidOperationException>(() => targetOwner.ValidateWorkflowAdmissionReplayAsync(
            historicalAdmission with { ImportedHistory = null }, projectId, workflowNode.Id, sourceAuthority, null, WorkflowPreviewSimulationPlan.Empty));
        await Assert.ThrowsAsync<InvalidOperationException>(() => targetOwner.DeferWorkflowAdmissionDeliveryAsync(admission.Binding.IntentId));
        var denied = await Assert.ThrowsAsync<ProjectStructureAgentException>(() => targetOwner.PrepareWorkflowAdmissionAsync(projectId, workflowNode.Id,
            admission.Binding.IntentId, true, definition, "{}", null, WorkflowPreviewSimulationPlan.Empty, sourceAuthority, actor));
        Assert.Equal(403, denied.StatusCode);
        Assert.Equal("WorkflowAuthorityDenied", denied.ErrorCode);
        Assert.Equal("The Workflow's original authority does not permit this exact project operation.", denied.Message);
        Assert.Equal(JsonSerializer.Serialize(historicalAdmission),
            JsonSerializer.Serialize(await targetOwner.FindWorkflowAdmissionAsync(admission.Binding.IntentId)));
        var status = new ProjectStructureWorkflowRunStatus(admission.Binding.RunId, WorkflowRunState.Completed, "Completed", "progress", 100,
            "", "", "", 1, 1, "Historical completion", new(admission.Binding.RunId, WorkflowRunState.Completed, "Fixture", "Historical completion", 1, 1, [], [], [], []), []);
        await Assert.ThrowsAsync<InvalidOperationException>(() => targetOwner.DeliverWorkflowStatusAsync(historicalAdmission, status, null, true));

        var targetAdmissions = fixture.TargetServices.GetRequiredService<ProjectWriteAdmissionService>();
        var forgedCurrentProfile = reserved with { DatabaseProfileId = fixture.TargetProfile.Profile.Id };
        await Assert.ThrowsAsync<InvalidOperationException>(() => targetAdmissions.RequireCreationGrantAsync(forgedCurrentProfile));
        await Assert.ThrowsAsync<InvalidOperationException>(() => targetAdmissions.CancelCreationAsync(forgedCurrentProfile));
        await Assert.ThrowsAsync<InvalidOperationException>(() => targetAdmissions.ReserveCreationAsync(reserved.ProjectId, reserved.RequesterId, reserved.Id));
        Assert.Empty((await targetAdmissions.ListAccessBindingFactsAsync([reserved.ProjectId])).Reservations);
        var rejected = await fixture.TargetServices.GetRequiredService<ProjectsService>().CreateAsync(forgedCurrentProfile, new() { Name = "Stale imported reservation" });
        Assert.Equal(ProjectErrorCodes.ReservationClosed, Assert.Single(rejected.Errors).Code);
        await using (var owner = await fixture.TargetServices.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync()) {
            await using var transaction = await owner.Database.BeginTransactionAsync();
            using var coordination = fixture.TargetServices.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(owner);
            Assert.Null(await targetAdmissions.CaptureReservedCreationForMutationAsync(reserved.ProjectId));
            await transaction.RollbackAsync();
        }
        var freshReservation = await targetAdmissions.ReserveCreationAsync(reserved.ProjectId, reserved.RequesterId, Guid.NewGuid());
        Assert.NotEqual(reserved.LifetimeId, freshReservation.LifetimeId);
        Assert.True((await fixture.TargetServices.GetRequiredService<ProjectsService>().CreateAsync(freshReservation, new() { Name = "Fresh target creation" })).IsSuccess);

        var targetAuthority = await fixture.TargetServices.GetRequiredService<ProjectStructureWorkflowAuthorityService>().CaptureAsync(projectId,
            ProjectStructureWorkflowAuthoritySource.LocalOperator(WorkflowStructureOperatorSurface.UserInterface));
        var freshAdmission = await targetOwner.PrepareWorkflowAdmissionAsync(projectId, workflowNode.Id, Guid.NewGuid(), false,
            definition, "{}", null, WorkflowPreviewSimulationPlan.Empty, targetAuthority, actor);
        Assert.Null(freshAdmission.ImportedHistory);
        Assert.Equal(freshAdmission.Binding.IntentId, Assert.Single(await targetOwner.ListWorkflowAdmissionsForDeliveryAsync(1)).Binding.IntentId);
        var freshPrepared = await PrepareWorkflowTaskContributionAsync(fixture.TargetServices, definition, projectId, request, targetAuthority);
        Assert.NotEqual(plan.ProjectLifetime, freshPrepared.Plan.ProjectLifetime);
        Assert.NotEqual(plan.SourceAuthorityFingerprint, freshPrepared.Plan.SourceAuthorityFingerprint);
        var fresh = await targetOwner.CreateWorkflowContributionAsync(freshPrepared.Plan, freshPrepared.Request);
        Assert.False(fresh.WasReplay);
        Assert.NotEqual(created.Node.Id, fresh.Node.Id);
        await using var target = await fixture.TargetContextAsync();
        var retained = await target.Set<ProjectCreationReservationRecord>().SingleAsync(row => row.Id == reserved.Id);
        Assert.Equal(ProjectCreationReservationState.Reserved, retained.State);
        Assert.NotNull(retained.ImportedHistory);
    }

    private static async Task<(WorkflowStructureOutputPlan Plan, ProjectObjectCreateRequest Request)> PrepareWorkflowTaskContributionAsync(
        IServiceProvider services, WorkflowDefinition definition, Guid projectId, ProjectObjectCreateRequest request, WorkflowStructureAuthority authority) {
        var owner = services.GetRequiredService<ProjectWorkbenchService>();
        var parent = request.ParentNodeKey!;
        var runId = WorkflowRunId.New();
        var lifetime = Assert.IsType<WorkflowProjectLifetime>(authority.ProjectScope!.Find(projectId));
        var plan = new WorkflowStructureOutputPlan(new(WorkflowExecutionOccurrence.Start(runId).Advance(definition.VersionId, new("effect")), 0),
            definition.VersionId, new("effect"), projectId, new(parent), await owner.ReadWorkflowTargetBindingAsync(projectId, parent),
            WorkflowStructureOutputKind.Task, WorkflowStructureOutputRole.RequiredResult, string.Empty) {
            ProjectLifetime = lifetime,
            SourceAuthorityFingerprint = WorkflowStructureAuthorityFingerprint.Create(authority)
        };
        plan = plan with { Fingerprint = ProjectWorkflowContributionFingerprint.Create(plan, request) };
        request = request with {
            ExpectedProjectAdmission = ProjectStructureWorkflowAuthorityService.ToProjectAdmission(lifetime),
            WorkflowMutationAdmission = new(authority, lifetime, WorkflowStructureAuthorityUse.TaskOutput, plan)
        };
        var run = new WorkflowRunSnapshot(runId, definition.Id, definition.VersionId, WorkflowRunState.Completed,
            WorkflowRuntimeBackendKind.InProcess, runId.ToString(), "Completed", Now, Now) {
            Origin = new WorkflowLaunchOrigin.Preview(authority.Principal, new("project-transfer-history-fixture")) { StructureAuthority = authority }
        };
        await services.GetRequiredService<IWorkflowRunStore>().SaveRunAsync(run);
        await services.GetRequiredService<IWorkflowStructureOutputStore>().PrepareAsync(plan);
        return (plan, request);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_Work_assignments_travel_as_exact_inert_history_without_copying_CRM_or_rebinding_lifetimes(bool package) {
        await using var fixture = await TransferFixture.CreateAsync();
        var project = new Project { Name = "Work source", CurrentPhase = "Execution" };
        var party = new Party { PartyType = PartyType.Person, DisplayName = "Source assignee" };
        var organization = new Party { PartyType = PartyType.Organization, DisplayName = "Source organization" };
        var affiliation = new PartyOrganizationAffiliation { PersonPartyId = party.Id, OrganizationPartyId = organization.Id };
        var opportunity = new Opportunity { AccountPartyId = organization.Id, OwnerPartyId = party.Id, Title = "Source opportunity" };
        var node = new ProjectObjectRecord { ProjectId = project.Id, NodeKey = "work:portable", ObjectType = ProjectObjectType.WorkItem,
            ObjectSubtype = "task", Title = "Portable native task", ParentNodeKey = ProjectWorkbenchGraphConventions.BuildProjectRootNodeKey(project.Id),
            MetadataJson = "{\"displaySnapshot\":\"Source assignee\",\"revision\":17}" };
        var assignment = new ProjectWorkAssignmentRecord { ProjectId = project.Id, ProjectLifetimeId = project.LifetimeId, PartyId = party.Id,
            PartyOrganizationAffiliationId = affiliation.Id, NodeKey = node.NodeKey, PhaseName = "Phase 2", OpportunityId = opportunity.Id,
            AllocationPercent = 37.125m, StartsAtUtc = Now.AddDays(-1), EndsAtUtc = Now.AddDays(2), IsPrimary = true,
            Source = "original-source", Notes = "  exact notes\nwith line break  " };
        var unbound = new ProjectWorkAssignmentRecord { ProjectId = project.Id, PartyId = party.Id, NodeKey = node.NodeKey,
            PhaseName = "Legacy", Source = "legacy", Notes = "No lifetime inferred" };
        var missingNode = new ProjectWorkAssignmentRecord { ProjectId = project.Id, ProjectLifetimeId = project.LifetimeId,
            PartyId = party.Id, NodeKey = "work:missing", Notes = "Preserve in source; no native reference exported" };
        await using (var source = await fixture.SourceContextAsync()) {
            source.AddRange(project, party, organization, affiliation, opportunity, node, assignment, unbound, missingNode);
            await source.SaveChangesAsync();
        }
        var sourceData = await fixture.LoadAsync(target: false);
        Assert.Equal(2, sourceData.WorkAssignmentHistory.Count);
        var expected = sourceData.WorkAssignmentHistory.OrderBy(row => row.EvidenceId).ToArray();
        if (package) {
            var service = fixture.PackageService();
            var exported = await service.ExportAllAsync(new() { PackagePath = Path.Combine(fixture.Environment.RootPath, "work.cdaproj") });
            Assert.True(exported.IsSuccess, string.Join("; ", exported.Errors.Select(error => error.Message)));
            Assert.Equal(ProjectPackageManifest.CurrentWorkAssignmentHistoryVersion, exported.Value!.Manifest.WorkAssignmentHistoryVersion);
            var imported = await service.ImportAllAsync(new() { PackagePath = exported.Value.PackagePath, TargetProfileId = fixture.TargetProfile.Profile.Id });
            Assert.True(imported.IsSuccess, string.Join("; ", imported.Errors.Select(error => error.Message)));
        } else {
            var result = await fixture.TransferAsync();
            Assert.True(result.Success, result.Message);
        }
        await fixture.RestartTargetAsync();
        await using var target = await fixture.TargetContextAsync();
        Assert.Empty(await target.Set<ProjectWorkAssignmentRecord>().ToArrayAsync());
        Assert.Empty(await target.Set<Party>().ToArrayAsync());
        Assert.Empty(await target.Set<PartyOrganizationAffiliation>().ToArrayAsync());
        Assert.Empty(await target.Set<Opportunity>().ToArrayAsync());
        Assert.NotEqual(project.LifetimeId, (await target.Set<Project>().SingleAsync()).LifetimeId);
        Assert.Equal(node.MetadataJson, (await target.Set<ProjectObjectRecord>().SingleAsync()).MetadataJson);
        var history = await target.Set<ProjectWorkAssignmentHistoryRecord>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync();
        Assert.Equal(expected.Select(row => row.PayloadJson), history.Select(row => row.PayloadJson));
        Assert.All(history, row => Assert.Equal(fixture.SourceProfile.Profile.Id, row.ImportedHistory.SourceProfileId));
        Assert.Contains(history, row => row.ProjectLifetimeId is null);
        var rebound = await fixture.LoadAsync(target: true);
        Assert.Equal(expected.Select(row => row.PayloadJson), rebound.WorkAssignmentHistory.OrderBy(row => row.EvidenceId).Select(row => row.PayloadJson));
        var originalProvenance = rebound.WorkAssignmentHistory.ToDictionary(row => row.EvidenceId, row => row.ImportedHistory);
        rebound.PrepareForTargetImport(fixture.TargetProfile.Profile.Id, Guid.NewGuid());
        Assert.Equal(expected.Select(row => row.PayloadJson), rebound.WorkAssignmentHistory.OrderBy(row => row.EvidenceId).Select(row => row.PayloadJson));
        Assert.All(rebound.WorkAssignmentHistory, row => Assert.Equal(originalProvenance[row.EvidenceId], row.ImportedHistory!.Previous));
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.ClearTargetAsync());
        await using var unchanged = await fixture.SourceContextAsync();
        Assert.Equal(3, await unchanged.Set<ProjectWorkAssignmentRecord>().CountAsync());
    }

    private static async Task<ProjectTransferDataSet> SeedEvidenceAsync(TransferFixture fixture, bool includeProject) {
        var project = new Project { Name = "Transfer source", Slug = "transfer-source", Description = "Legacy content", Objective = "Keep history", CreatedAtUtc = Now, UpdatedAtUtc = Now };
        var data = new ProjectTransferDataSet {
            Retirements = [new() { LifetimeId = Guid.NewGuid(), ProjectId = Guid.NewGuid(), RetiredAtUtc = Now,
                ImportedHistory = new(Guid.NewGuid(), Guid.NewGuid()) }] };
        foreach (var state in Enum.GetValues<ProjectCreationReservationState>()) {
            data.CreationReservations.Add(new() { Id = Guid.NewGuid(), DatabaseProfileId = fixture.SourceProfile.Profile.Id,
                ProjectId = Guid.NewGuid(), LifetimeId = Guid.NewGuid(), RequesterId = Guid.NewGuid(), State = state, CreatedAtUtc = Now,
                ConsumedAtUtc = state == ProjectCreationReservationState.Consumed ? Now : null,
                CancelledAtUtc = state == ProjectCreationReservationState.Cancelled ? Now : null });
        }
        for (var index = 0; index < 2; index++) {
            data.WorkflowContributions.Add(new() { RunId = Guid.NewGuid(), ProjectId = project.Id, OccurrencePath = $"path-{index}", Slot = index,
                DatabaseProfileId = index == 0 ? null : fixture.SourceProfile.Profile.Id, ProjectLifetimeId = index == 0 ? null : project.LifetimeId,
                NativeObjectId = index == 0 ? Guid.Empty : Guid.NewGuid(), PreparedAtUtc = Now, PlanJson = " original plan ",
                RequestJson = " original request ", ReceiptJson = index == 0 ? string.Empty : " original receipt ", NodeJson = " original node " });
        }
        var sequence = 0L;
        foreach (var state in Enum.GetValues<ProjectWorkflowDeliveryState>()) {
            data.WorkflowAdmissions.Add(new() { IntentId = Guid.NewGuid(), ProjectId = project.Id, NodeId = "deleted-workflow", NativeNodeId = Guid.NewGuid(),
                DatabaseProfileId = sequence % 2 == 0 ? fixture.SourceProfile.Profile.Id : null,
                ProjectLifetimeId = sequence % 2 == 0 ? project.LifetimeId : null,
                RunId = Guid.NewGuid(), Sequence = ++sequence, AdmissionJson = " original authority ", Delivery = state,
                StatusJson = " original status ", NextAttemptAtUtc = Now, ObservedRunUpdatedAtUtc = Now,
                DeliveryFinished = state == ProjectWorkflowDeliveryState.Applied });
        }
        await using var source = await fixture.SourceContextAsync();
        if (includeProject) {
            source.Add(project);
        }
        source.AddRange(data.Retirements.Select(row => JsonSerializer.Deserialize<ProjectRetirementRecord>(JsonSerializer.Serialize(row))!));
        source.AddRange(data.CreationReservations.Select(row => JsonSerializer.Deserialize<ProjectCreationReservationRecord>(JsonSerializer.Serialize(row))!));
        source.AddRange(data.WorkflowContributions);
        source.AddRange(data.WorkflowAdmissions);
        await source.SaveChangesAsync();
        return await fixture.LoadAsync(target: false);
    }

    private static IEnumerable<RetainedEvidenceImport?> Provenance(ProjectTransferDataSet data) => data.Retirements.Select(row => row.ImportedHistory)
        .Concat(data.CreationReservations.Select(row => row.ImportedHistory)).Concat(data.WorkflowContributions.Select(row => row.ImportedHistory))
        .Concat(data.WorkflowAdmissions.Select(row => row.ImportedHistory));

    private static string EvidenceSnapshot(ProjectTransferDataSet data) => JsonSerializer.Serialize(new {
        Retirements = data.Retirements.OrderBy(row => row.LifetimeId).Select(WithoutProvenance),
        Reservations = data.CreationReservations.OrderBy(row => row.Id).Select(WithoutProvenance),
        Contributions = data.WorkflowContributions.OrderBy(row => row.RunId).Select(WithoutProvenance),
        Admissions = data.WorkflowAdmissions.OrderBy(row => row.IntentId).Select(WithoutProvenance)
    });

    private static string WithoutProvenance<T>(T value) {
        var json = JsonSerializer.SerializeToNode(value)!.AsObject();
        json.Remove(nameof(ProjectRetirementRecord.ImportedHistory));
        return json.ToJsonString();
    }

    private static async Task<string> MakeLegacyPackageAsync(string original, string root, bool legacyFormat = true) {
        var copy = Path.Combine(root, "legacy.cdaproj");
        File.Copy(original, copy);
        using var archive = ZipFile.Open(copy, ZipArchiveMode.Update);
        var entry = archive.GetEntry("manifest.json")!;
        ProjectPackageManifest manifest;
        using (var reader = new StreamReader(entry.Open())) {
            manifest = JsonSerializer.Deserialize<ProjectPackageManifest>(await reader.ReadToEndAsync(), PackageJson)!;
        }
        string[] historyTables = ["Projects_ProjectRetirements", "Projects_ProjectCreationReservations", "Workbench_WorkflowContributionReceipts", "Workbench_WorkflowAdmissions", "Workbench_WorkAssignmentHistory", "Workbench_ProcessAssetContributions"];
        foreach (var table in manifest.Tables.Where(table => historyTables.Contains(table.Name) && (legacyFormat || table.Name is "Workbench_WorkAssignmentHistory" or "Workbench_ProcessAssetContributions")).ToArray()) {
            archive.GetEntry(table.FilePath)!.Delete();
            manifest.TotalRecordCount -= table.RowCount;
            manifest.Tables.Remove(table);
        }
        manifest.Format = legacyFormat ? ProjectPackageManifest.LegacyFormat : ProjectPackageManifest.CurrentFormat;
        manifest.HistoryDisposition = legacyFormat ? null : ProjectPackageHistoryDisposition.PreserveAsHistory;
        manifest.WorkAssignmentHistoryVersion = null;
        manifest.ProcessAssetHistoryVersion = null;
        entry.Delete();
        using var writer = new StreamWriter(archive.CreateEntry("manifest.json").Open());
        await writer.WriteAsync(JsonSerializer.Serialize(manifest, PackageJson));
        return copy;
    }

    private static WorkflowDefinition Definition() {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        WorkflowNode Node(WorkflowNodeId id, WorkflowNodeKind kind) => new(id, kind, id.Value, [],
            new(null, null, null, null, string.Empty, WorkflowValueShape.Text, WorkflowValueShape.Text));
        return new(WorkflowId.New(), WorkflowVersionId.New(), "Transfer fixture", "History", WorkflowLifecycleStatus.Active,
            new(start, [Node(start, WorkflowNodeKind.Start), Node(end, WorkflowNodeKind.End)],
                [new(new("start-end"), start, null, end, null, WorkflowEdgeKind.Direct, string.Empty)]),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false), Now, Now);
    }

    private sealed class TransferFixture : IAsyncDisposable {
        private readonly TestHarnessOptions targetOptions;
        private readonly TestApplication source;
        private TestApplication target;
        private readonly AsyncServiceScope sourceScope;
        private AsyncServiceScope targetScope;

        private TransferFixture(CanDoItAllTestEnvironment environment, TestApplication source, TestApplication target, TestHarnessOptions targetOptions) {
            Environment = environment;
            this.source = source;
            this.target = target;
            this.targetOptions = targetOptions;
            sourceScope = source.Services.CreateAsyncScope();
            targetScope = target.Services.CreateAsyncScope();
        }

        public CanDoItAllTestEnvironment Environment { get; }
        public IServiceProvider SourceServices => sourceScope.ServiceProvider;
        public IServiceProvider TargetServices => targetScope.ServiceProvider;
        public ResolvedDatabaseProfile SourceProfile => SourceServices.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        public ResolvedDatabaseProfile TargetProfile => TargetServices.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;

        public static async Task<TransferFixture> CreateAsync() {
            var environment = CanDoItAllTestEnvironment.Create("project-history-transfer");
            var sourceProfile = environment.CreatePostgreSqlProfile("source");
            var targetProfile = environment.CreatePostgreSqlProfile("target");
            var sourceOptions = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = sourceProfile, ConfigureServices = RemoveDelivery };
            var targetOptions = new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = targetProfile, ConfigureServices = RemoveDelivery };
            TestApplication? source = null;
            try {
                source = await TestApplication.CreateAsync(sourceOptions);
                return new(environment, source, await TestApplication.CreateAsync(targetOptions), targetOptions);
            } catch {
                if (source is not null) {
                    await source.DisposeAsync();
                }
                await environment.DisposeAsync();
                throw;
            }
        }

        public Task<AppDbContext> SourceContextAsync() => SourceServices.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();
        public Task<AppDbContext> TargetContextAsync() => TargetServices.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

        public ProjectPackageService PackageService() => ActivatorUtilities.CreateInstance<ProjectPackageService>(SourceServices,
            new Profiles(SourceProfile, TargetProfile));

        public async Task<DatabaseTransferItemResult> TransferAsync(IInterceptor? interceptor = null, IInterceptor? sourceProbe = null) {
            var sourceOptions = new DbContextOptionsBuilder<AppDbContext>(SourceServices.GetRequiredService<DbContextOptions<AppDbContext>>());
            if (sourceProbe is not null) {
                sourceOptions.AddInterceptors(sourceProbe);
            }
            await using var sourceContext = new AppDbContext(sourceOptions.Options);
            var options = new DbContextOptionsBuilder<AppDbContext>(TargetServices.GetRequiredService<DbContextOptions<AppDbContext>>());
            if (interceptor is not null) {
                options.AddInterceptors(interceptor);
            }
            await using var targetContext = new AppDbContext(options.Options);
            var transfer = new DatabaseTransferOperation(SourceProfile, TargetProfile, true);
            var inspections = SourceServices.GetRequiredService<ProjectTransferTargetInspectionRunner>();
            var operations = DatabaseTransferTestSupport.For(transfer, sourceContext, targetContext, inspections: inspections);
            var guard = new ProjectTransferTargetStateGuard(SourceServices.GetRequiredService<IEnumerable<IProjectTransferTargetStateParticipant>>(), operations);
            var handler = new ProjectsDatabaseTransferHandler(new Profiles(SourceProfile, TargetProfile), guard, operations, new(operations));
            return await handler.TransferAsync(transfer);
        }

        public Task<ProjectTransferDataSet> LoadAsync(bool target) {
            var operations = SourceServices.GetRequiredService<DatabaseTransferOperationRunner>();
            var store = new ProjectTransferStore(operations, new(operations));
            return operations.RunIndependentAsync(target ? TargetProfile : SourceProfile, store.LoadAsync);
        }

        public Task<bool> ClearTargetAsync() {
            var operations = SourceServices.GetRequiredService<DatabaseTransferOperationRunner>();
            var store = new ProjectTransferStore(operations, new(operations));
            return operations.RunSerializableAsync(TargetProfile, [ProjectStructureSerializableMutationScope.ManagedStorageBindingScopeKey], async (session, token) => {
                await store.ClearAsync(session, token);
                return true;
            });
        }

        public async Task RestartTargetAsync() {
            await targetScope.DisposeAsync();
            await target.DisposeAsync();
            target = await TestApplication.CreateAsync(targetOptions);
            targetScope = target.Services.CreateAsyncScope();
        }

        public async ValueTask DisposeAsync() {
            await targetScope.DisposeAsync();
            await sourceScope.DisposeAsync();
            await target.DisposeAsync();
            await source.DisposeAsync();
            await Environment.DisposeAsync();
        }

        private static void RemoveDelivery(IServiceCollection services) {
            foreach (var descriptor in services.Where(value => value.ImplementationType == typeof(ProjectStructureWorkflowDeliveryWorker)).ToArray()) {
                services.Remove(descriptor);
            }
        }
    }

    private sealed class Profiles(ResolvedDatabaseProfile source, ResolvedDatabaseProfile target) : IDatabaseProfileRuntimeAccessor {
        public ResolvedDatabaseProfile ResolveCurrentProfile() => source;
        public ResolvedDatabaseProfile ResolveProfile(Guid profileId) => profileId == source.Profile.Id ? source
            : profileId == target.Profile.Id ? target : throw new KeyNotFoundException();
    }

    private sealed class InjectedTransferFailure : Exception { }

    private sealed class SourceSnapshotProbe : DbCommandInterceptor {
        public List<IsolationLevel?> ReadTransactions { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)) {
                ReadTransactions.Add(command.Transaction?.IsolationLevel);
            }
            return ValueTask.FromResult(result);
        }
    }

    private sealed class RejectAdmissionInsert : SaveChangesInterceptor {
        public bool Observed { get; private set; }
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<ProjectWorkflowAdmissionRecord>().Any(entry => entry.State == EntityState.Added)) {
                Observed = true;
                throw new InjectedTransferFailure();
            }
            return ValueTask.FromResult(result);
        }
    }
}
