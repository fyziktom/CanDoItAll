using System.Data;
using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class WorkAssignmentAdmissionIntegrationTests {
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(ProjectPartyAssignmentRole.WorkItemAssignee)]
    [InlineData(ProjectPartyAssignmentRole.Manager)]
    public async Task Captured_save_and_empty_replacement_reject_recreated_project_after_restart(ProjectPartyAssignmentRole role) {
        await using var environment = CanDoItAllTestEnvironment.Create("assignment-admission-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        Seed old;
        Seed current;
        Guid currentAssignmentId;
        await using (var app = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile })) {
            old = await SeedAsync(app);
            current = await RecreateAsync(app, old);
            await using var scope = app.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>().SaveAssignmentAsync(Request(current, role));
            Assert.True(result.IsSuccess);
            currentAssignmentId = result.Value;
        }
        await using var restarted = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile });
        await using var restartedScope = restarted.Services.CreateAsyncScope();
        var commands = restartedScope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => commands.SaveAssignmentAsync(Request(old, role)));
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => commands.ReplaceNodeAssignmentsAsync(
            old.ProjectId, new(old.NodeKey), [], [role], expectedProjectAdmission: old.Admission));
        await using (var complete = await CanonicalAsync(restarted)) {
            Assert.True(await ExistsAsync(complete, currentAssignmentId, role));
        }
        Assert.True((await commands.ReplaceNodeAssignmentsAsync(current.ProjectId, new(current.NodeKey), [], [role],
            expectedProjectAdmission: current.Admission)).IsSuccess);
        await using var verified = await CanonicalAsync(restarted);
        Assert.False(await ExistsAsync(verified, currentAssignmentId, role));
    }

    [Fact]
    public async Task Retained_cleanup_removes_only_recorded_incarnation_and_does_not_reprice_recreated_task() {
        await using var app = await TestApplication.CreateAsync();
        var old = await SeedAsync(app);
        var current = await RecreateAsync(app, old);
        var oldWork = Work(old);
        var newWork = Work(current);
        var oldCrm = Participation(old);
        var newCrm = Participation(current);
        await using (var complete = await CanonicalAsync(app)) {
            complete.AddRange(oldWork, newWork, oldCrm, newCrm);
            await complete.SaveChangesAsync();
        }
        await using var scope = app.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var page = await commands.SearchAssignmentsDetailedAsync(new(current.ProjectId,
            [ProjectPartyAssignmentRole.Manager, ProjectPartyAssignmentRole.WorkItemAssignee], PageIndex: 1, PageSize: 1));
        Assert.Equal(2, page.TotalCount);
        Assert.Equal(current.Admission.LifetimeId, Assert.Single(page.Items).ProjectLifetimeId);
        await commands.DeleteAssignmentsForNodesAsync(old.ProjectId, [new(old.NodeKey)],
            expectedReference: ProjectAssignmentReference.From(old.Admission));
        await using var verified = await CanonicalAsync(app);
        Assert.False(await verified.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.Id == oldWork.Id));
        Assert.False(await verified.Set<ProjectPartyAssignment>().AnyAsync(item => item.Id == oldCrm.Id));
        Assert.Equal(newWork.ProjectLifetimeId, (await verified.Set<ProjectWorkAssignmentRecord>().SingleAsync(item => item.Id == newWork.Id)).ProjectLifetimeId);
        Assert.Equal(newCrm.ProjectLifetimeId, (await verified.Set<ProjectPartyAssignment>().SingleAsync(item => item.Id == newCrm.Id)).ProjectLifetimeId);
        var task = await verified.Set<ProjectObjectRecord>().SingleAsync(item => item.ProjectId == current.ProjectId);
        Assert.Equal(0, ProjectObjectMetadataSerializer.Parse(task.MetadataJson).WorkItem!.DirectAssignmentRevision);
    }

    [Fact]
    public async Task Unbound_cleanup_refuses_without_deleting_legacy_or_current_rows() {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        var legacy = Work(seed);
        legacy.ProjectLifetimeId = null;
        var current = Participation(seed);
        await using (var complete = await CanonicalAsync(app)) {
            complete.AddRange(legacy, current);
            await complete.SaveChangesAsync();
        }
        await using var scope = app.Services.CreateAsyncScope();
        await Assert.ThrowsAsync<InvalidOperationException>(() => scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>()
            .DeleteAssignmentsForProjectAsync(seed.ProjectId,
                expectedReference: new(seed.Admission.DatabaseProfileId, seed.ProjectId, null)));
        await using var verified = await CanonicalAsync(app);
        Assert.True(await verified.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.Id == legacy.Id));
        Assert.True(await verified.Set<ProjectPartyAssignment>().AnyAsync(item => item.Id == current.Id));
    }

    [Fact]
    public async Task Delayed_move_rejects_recreated_target_and_receipt_cannot_authorize_another_lifetime() {
        await using var app = await TestApplication.CreateAsync();
        var source = await SeedAsync(app);
        var oldTarget = await SeedAsync(app);
        var target = await RecreateAsync(app, oldTarget);
        var work = Work(source);
        var crm = Participation(source);
        await using (var complete = await CanonicalAsync(app)) {
            complete.AddRange(work, crm);
            await complete.SaveChangesAsync();
        }
        await using var scope = app.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var operation = new ProjectPartyAssignmentMoveOperationId(Guid.NewGuid());
        var sourceReference = ProjectAssignmentReference.From(source.Admission);
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => commands.MoveAssignmentsToProjectAsync(
            operation, source.ProjectId, [new(source.NodeKey)], target.ProjectId,
            sourceReference: sourceReference, expectedTargetAdmission: oldTarget.Admission));
        await using (var complete = await CanonicalAsync(app)) {
            Assert.Empty(await complete.Set<ProjectPartyAssignmentMoveReceipt>().ToListAsync());
            Assert.Equal(source.ProjectId, (await complete.Set<ProjectWorkAssignmentRecord>().SingleAsync(item => item.Id == work.Id)).ProjectId);
        }
        await commands.MoveAssignmentsToProjectAsync(operation, source.ProjectId, [new(source.NodeKey)], target.ProjectId,
            sourceReference: sourceReference, expectedTargetAdmission: target.Admission);
        await commands.MoveAssignmentsToProjectAsync(operation, source.ProjectId, [new(source.NodeKey)], target.ProjectId,
            sourceReference: sourceReference, expectedTargetAdmission: target.Admission);
        await Assert.ThrowsAsync<InvalidOperationException>(() => commands.MoveAssignmentsToProjectAsync(
            operation, source.ProjectId, [new(source.NodeKey)], target.ProjectId,
            sourceReference: sourceReference, expectedTargetAdmission: oldTarget.Admission));
        await using var verified = await CanonicalAsync(app);
        var moved = await verified.Set<ProjectWorkAssignmentRecord>().SingleAsync(item => item.Id == work.Id);
        Assert.Equal(target.Admission.LifetimeId, moved.ProjectLifetimeId);
        Assert.Equal(work.PhaseName, moved.PhaseName);
        Assert.Equal(work.OpportunityId, moved.OpportunityId);
        Assert.Equal(work.Notes, moved.Notes);
        var receipt = Assert.Single(await verified.Set<ProjectPartyAssignmentMoveReceipt>().ToListAsync());
        Assert.Equal(source.Admission.LifetimeId, receipt.SourceProjectLifetimeId);
        Assert.Equal(target.Admission.LifetimeId, receipt.TargetProjectLifetimeId);
        Assert.Equal(source.Admission.DatabaseProfileId, receipt.DatabaseProfileId);
    }

    [Fact]
    public async Task Exact_staging_rolls_back_bound_assignment_and_native_revision_with_caller_transaction() {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        await using var scope = app.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentCommands>();
        var identity = Guid.NewGuid();
        await using var owner = await app.Services.GetRequiredService<IDbContextFactory<CrmHrDbContext>>().CreateDbContextAsync();
        await using var transaction = await owner.Database.BeginTransactionAsync(IsolationLevel.Serializable);
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(owner,
            [ProjectMutationScopeKeys.ForProject(seed.ProjectId), ProjectAssignmentMutationKeys.ForAssignment(identity)], CancellationToken.None);
        using (app.Services.GetRequiredService<CoordinatedDatabaseTransaction>().Enter(owner)) {
            Assert.True((await commands.StageSaveAsync(Request(seed, ProjectPartyAssignmentRole.WorkItemAssignee), identity)).IsSuccess);
            Assert.Equal(seed.Admission.LifetimeId, (await commands.GetForMutationAsync(identity))!.ProjectLifetimeId);
            Assert.Null(await commands.GetAsync(identity));
        }
        await transaction.RollbackAsync();
        Assert.Null(await commands.GetAsync(identity));
        await using var complete = await CanonicalAsync(app);
        Assert.Equal(0, ProjectObjectMetadataSerializer.Parse((await complete.Set<ProjectObjectRecord>()
            .SingleAsync(item => item.ProjectId == seed.ProjectId)).MetadataJson).WorkItem!.DirectAssignmentRevision);
    }

    [Fact]
    public async Task Cross_role_same_identity_preserves_all_mutable_fields_and_lifetime() {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        var initial = Participation(seed);
        initial.PhaseName = "Retained phase";
        initial.OpportunityId = Guid.NewGuid();
        await using (var complete = await CanonicalAsync(app)) {
            complete.Add(initial);
            await complete.SaveChangesAsync();
        }
        await using var scope = app.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<ProjectPartyIntegrationService>();
        var request = Request(seed, ProjectPartyAssignmentRole.WorkItemAssignee);
        request.AssignmentId = initial.Id;
        Assert.True((await commands.SaveAssignmentAsync(request)).IsSuccess);
        await using (var complete = await CanonicalAsync(app)) {
            var work = await complete.Set<ProjectWorkAssignmentRecord>().SingleAsync(item => item.Id == initial.Id);
            Assert.Equal(seed.Admission.LifetimeId, work.ProjectLifetimeId);
            Assert.Equal(initial.PhaseName, work.PhaseName);
            Assert.Equal(initial.OpportunityId, work.OpportunityId);
            Assert.Equal(request.AllocationPercent, work.AllocationPercent);
            Assert.Equal(request.Source, work.Source);
            Assert.Equal(request.Notes, work.Notes);
            Assert.False(await complete.Set<ProjectPartyAssignment>().AnyAsync(item => item.Id == initial.Id));
        }
        request.Role = ProjectPartyAssignmentRole.Manager;
        Assert.True((await commands.SaveAssignmentAsync(request)).IsSuccess);
        await using var verified = await CanonicalAsync(app);
        var restored = await verified.Set<ProjectPartyAssignment>().SingleAsync(item => item.Id == initial.Id);
        Assert.Equal(seed.Admission.LifetimeId, restored.ProjectLifetimeId);
        Assert.Equal(initial.PhaseName, restored.PhaseName);
        Assert.Equal(initial.OpportunityId, restored.OpportunityId);
        Assert.False(await verified.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.Id == initial.Id));
    }

    [Fact]
    public async Task Staffing_global_save_remains_supported_and_delayed_project_save_cannot_rebind() {
        await using var app = await TestApplication.CreateAsync();
        var old = await SeedAsync(app);
        var current = await RecreateAsync(app, old);
        await using var scope = app.Services.CreateAsyncScope();
        var hr = scope.ServiceProvider.GetRequiredService<HrService>();
        Assert.True((await hr.SaveStaffingRequestAsync(new StaffingRequestEditorModel { Title = "Global request", NeededRole = "Engineer" })).IsSuccess);
        var delayed = new StaffingRequestEditorModel {
            ProjectId = old.ProjectId, ExpectedProjectAdmission = old.Admission, Title = "Delayed request", NeededRole = "Engineer"
        };
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => hr.SaveStaffingRequestAsync(delayed));
        delayed.ExpectedProjectAdmission = current.Admission;
        var saved = await hr.SaveStaffingRequestAsync(delayed);
        Assert.True(saved.IsSuccess);
        await using (var complete = await CanonicalAsync(app)) {
            Assert.Null((await complete.Set<StaffingRequest>().SingleAsync(item => item.ProjectId == null)).ProjectLifetimeId);
            Assert.Equal(current.Admission.LifetimeId, (await complete.Set<StaffingRequest>().SingleAsync(item => item.ProjectId == current.ProjectId)).ProjectLifetimeId);
        }
        delayed.Id = saved.Value;
        delayed.ProjectId = null;
        delayed.ExpectedProjectAdmission = null;
        await Assert.ThrowsAsync<InvalidOperationException>(() => hr.SaveStaffingRequestAsync(delayed));
        delayed.ExpectedSourceProjectReference = ProjectAssignmentReference.From(current.Admission);
        Assert.True((await hr.SaveStaffingRequestAsync(delayed)).IsSuccess);
        await using var unassigned = await CanonicalAsync(app);
        var global = await unassigned.Set<StaffingRequest>().SingleAsync(item => item.Id == saved.Value);
        Assert.Null(global.ProjectId);
        Assert.Null(global.ProjectLifetimeId);
    }

    [Fact]
    public async Task Owner_rejects_foreign_profile_token_and_keeps_legacy_orphan_history_readable() {
        await using var app = await TestApplication.CreateAsync();
        var seed = await SeedAsync(app);
        var request = Request(seed, ProjectPartyAssignmentRole.WorkItemAssignee);
        request.ExpectedProjectAdmission = new(Guid.NewGuid(), seed.ProjectId, seed.Admission.LifetimeId);
        await using var scope = app.Services.CreateAsyncScope();
        var commands = scope.ServiceProvider.GetRequiredService<IProjectWorkAssignmentCommands>();
        await Assert.ThrowsAsync<ProjectWriteAdmissionRejectedException>(() => commands.SaveAsync(request));
        var orphan = Work(seed);
        orphan.ProjectId = Guid.NewGuid();
        orphan.ProjectLifetimeId = null;
        await using (var complete = await CanonicalAsync(app)) {
            complete.Add(orphan);
            await complete.SaveChangesAsync();
        }
        var restored = await commands.GetAsync(orphan.Id);
        Assert.NotNull(restored);
        Assert.Null(restored.ProjectLifetimeId);
        Assert.Equal(orphan.Notes, restored.Notes);
        Assert.Equal(ProjectPartyAssignmentRole.WorkItemAssignee, restored.Role);
    }

    [Fact]
    public async Task Durable_cleanup_replay_after_restart_keeps_recreated_assignments_and_original_payload_scope() {
        await using var environment = CanDoItAllTestEnvironment.Create("assignment-cleanup-restart");
        var profile = environment.CreatePostgreSqlProfile("original");
        Guid mutationId;
        Guid oldId;
        Guid currentId;
        ProjectAssignmentReference reference;
        await using (var app = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile })) {
            var old = await SeedAsync(app);
            var current = await RecreateAsync(app, old);
            reference = ProjectAssignmentReference.From(old.Admission);
            var oldRow = Work(old);
            var currentRow = Work(current);
            oldId = oldRow.Id;
            currentId = currentRow.Id;
            var mutation = new ProjectCrossModuleMutationRecord {
                ProjectId = old.ProjectId, ScopeNodeKey = old.NodeKey, MutationKind = ProjectCrossModuleMutationKind.DeleteSubtree,
                Status = ProjectCrossModuleMutationStatus.WorkbenchCommitted, CreatedAtUtc = Now, UpdatedAtUtc = Now,
                PayloadJson = JsonSerializer.Serialize(new DeleteSubtreeMutationPayload(old.NodeKey, [old.NodeKey], 0,
                    SourceReference: reference), new JsonSerializerOptions(JsonSerializerDefaults.Web))
            };
            mutationId = mutation.Id;
            await using var complete = await CanonicalAsync(app);
            complete.AddRange(oldRow, currentRow, mutation);
            await complete.SaveChangesAsync();
        }
        await using var restarted = await TestApplication.CreateAsync(new TestHarnessOptions { TestEnvironment = environment, ActiveProfile = profile });
        await using var scope = restarted.Services.CreateAsyncScope();
        var processor = scope.ServiceProvider.GetRequiredService<ProjectCrossModuleMutationProcessor>();
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, await processor.ProcessAsync(mutationId));
        Assert.Equal(ProjectCrossModuleMutationStatus.Completed, await processor.ProcessAsync(mutationId));
        await using var verified = await CanonicalAsync(restarted);
        Assert.False(await verified.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.Id == oldId));
        Assert.True(await verified.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.Id == currentId));
        var persisted = await verified.Set<ProjectCrossModuleMutationRecord>().SingleAsync(item => item.Id == mutationId);
        Assert.Equal(reference, JsonSerializer.Deserialize<DeleteSubtreeMutationPayload>(persisted.PayloadJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))!.SourceReference);
    }

    private static async Task<Seed> SeedAsync(TestApplication app) {
        var project = new Project { Name = "Assignment admission", Slug = Guid.NewGuid().ToString("N") };
        var party = new Party { PartyType = PartyType.Person, DisplayName = "Assignment person" };
        await using var complete = await CanonicalAsync(app);
        complete.AddRange(project, party, Node(project.Id));
        await complete.SaveChangesAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var admission = await scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(project.Id);
        return new(project.Id, party.Id, NodeKey, admission!);
    }

    private static async Task<Seed> RecreateAsync(TestApplication app, Seed old) {
        await using var complete = await CanonicalAsync(app);
        await using var transaction = await complete.Database.BeginTransactionAsync();
        complete.RemoveRange(await complete.Set<ProjectObjectRecord>().Where(item => item.ProjectId == old.ProjectId).ToListAsync());
        complete.Remove(await complete.Set<Project>().SingleAsync(item => item.Id == old.ProjectId));
        complete.Add(new ProjectRetirementRecord { ProjectId = old.ProjectId, LifetimeId = old.Admission.LifetimeId, RetiredAtUtc = Now });
        await complete.SaveChangesAsync();
        var recreated = new Project { Id = old.ProjectId, Name = "Recreated assignment project", Slug = Guid.NewGuid().ToString("N") };
        complete.AddRange(recreated, Node(recreated.Id));
        await complete.SaveChangesAsync();
        await transaction.CommitAsync();
        Assert.NotEqual(old.Admission.LifetimeId, recreated.LifetimeId);
        return old with { Admission = new(old.Admission.DatabaseProfileId, old.ProjectId, recreated.LifetimeId) };
    }

    private const string NodeKey = "work-item:assignment-admission";

    private static ProjectObjectRecord Node(Guid projectId) => new() {
        ProjectId = projectId, NodeKey = NodeKey, ObjectType = ProjectObjectType.WorkItem, ObjectSubtype = "task",
        Title = "Task", CreatedAtUtc = Now, UpdatedAtUtc = Now,
        MetadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope {
            WorkItem = new ProjectWorkItemMetadata { WorkItemKind = ProjectWorkItemKind.Task, ExecutionState = ProjectTaskExecutionState.NotStarted }
        })
    };

    private static ProjectPartyAssignmentUpsertRequest Request(Seed seed, ProjectPartyAssignmentRole role) => new() {
        ProjectId = seed.ProjectId, ExpectedProjectAdmission = seed.Admission, PartyId = seed.PartyId,
        NodeKey = seed.NodeKey, Role = role, IsPrimary = true, AllocationPercent = 31.25m,
        StartsOn = new(2026, 9, 10), EndsOn = new(2026, 9, 12), Source = "captured editor", Notes = "Preserved notes"
    };

    private static ProjectWorkAssignmentRecord Work(Seed seed) => new() {
        ProjectId = seed.ProjectId, ProjectLifetimeId = seed.Admission.LifetimeId, PartyId = seed.PartyId,
        NodeKey = seed.NodeKey, PhaseName = "Original phase", OpportunityId = Guid.NewGuid(),
        AllocationPercent = 31.25m, StartsAtUtc = Now, EndsAtUtc = Now.AddDays(2), IsPrimary = true,
        Source = "Original source", Notes = "Original notes"
    };

    private static ProjectPartyAssignment Participation(Seed seed) => new() {
        ProjectId = seed.ProjectId, ProjectLifetimeId = seed.Admission.LifetimeId, PartyId = seed.PartyId,
        NodeKey = seed.NodeKey, AssignmentKind = ProjectPartyAssignmentKind.Manager, Source = "Original participation"
    };

    private static Task<bool> ExistsAsync(AppDbContext context, Guid id, ProjectPartyAssignmentRole role) => role == ProjectPartyAssignmentRole.WorkItemAssignee
        ? context.Set<ProjectWorkAssignmentRecord>().AnyAsync(item => item.Id == id)
        : context.Set<ProjectPartyAssignment>().AnyAsync(item => item.Id == id);

    private static Task<AppDbContext> CanonicalAsync(TestApplication app) => app.Services.GetRequiredService<IDbContextFactory<AppDbContext>>().CreateDbContextAsync();

    private sealed record Seed(Guid ProjectId, Guid PartyId, string NodeKey, ProjectWriteAdmission Admission);
}
