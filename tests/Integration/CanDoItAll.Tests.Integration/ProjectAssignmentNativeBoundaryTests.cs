using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class ProjectAssignmentNativeBoundaryTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Native_commit_authority_and_accepted_callback_failure_have_distinct_physical_outcomes(bool work, bool afterCommit) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var draft = await PrepareAsync(services, work);
        draft.Notes = "  Accepted normalized assignment  ";
        var changes = services.GetRequiredService<ProjectAssignmentChanges>();
        using var hints = changes.Subscribe(draft.ProjectId);
        ProjectAssignmentSaveReceipt? accepted = null;
        var checks = 0;
        using var authority = changes.EnterUiWrite(_ => {
            if (!afterCommit && ++checks == 3) {
                throw new UnauthorizedAccessException("Grant revoked after the native SQL flush.");
            }
            return ValueTask.CompletedTask;
        });
        void OnCommitted(ProjectAssignmentSaveReceipt receipt) {
            accepted = receipt;
            throw new DbUpdateConcurrencyException("Accepted callback failed after physical commit.");
        }

        var failure = await Record.ExceptionAsync(() => work
            ? services.GetRequiredService<IProjectWorkAssignmentCommands>().SaveAsync(draft, onCommitted: OnCommitted)
            : services.GetRequiredService<ProjectPartyIntegrationService>().SaveAssignmentAsync(draft, onCommitted: OnCommitted));

        var rows = await services.GetRequiredService<ProjectPartyIntegrationService>().ListAssignmentsDetailedAsync(draft.ProjectId);
        if (afterCommit) {
            Assert.IsType<DbUpdateConcurrencyException>(failure);
            Assert.NotNull(accepted);
            var row = Assert.Single(rows);
            Assert.Equal(accepted.Accepted, row.Original);
            Assert.Equal("Accepted normalized assignment", accepted.Accepted.Notes);
        } else {
            Assert.IsType<UnauthorizedAccessException>(failure);
            Assert.Equal(3, checks);
            Assert.Null(accepted);
            Assert.Empty(rows);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_owners_reject_stale_replacement_duplicate_create_and_missing_identity(bool work) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var draft = await PrepareAsync(services, work);
        var created = await SaveAsync(services, work, draft);
        Assert.True(created.IsSuccess);
        var shown = await ReadAsync(services, work, draft.ProjectId, created.Value);
        var peer = From(shown, draft.ExpectedProjectAdmission!);
        peer.Notes = "Accepted peer assignment";
        peer.AllocationPercent = 65m;
        Assert.True((await SaveAsync(services, work, peer)).IsSuccess);

        var stale = From(shown, draft.ExpectedProjectAdmission!);
        stale.Notes = "Stale native replacement";
        Assert.Equal(ProjectAssignmentEditingErrors.Conflict,
            Assert.Single((await SaveAsync(services, work, stale)).Errors).Code);
        Assert.Equal(ProjectAssignmentEditingErrors.Duplicate,
            Assert.Single((await SaveAsync(services, work, draft)).Errors).Code);
        var missing = From(shown with { Id = Guid.NewGuid() }, draft.ExpectedProjectAdmission!);
        Assert.True((await SaveAsync(services, work, missing)).IsFailure);

        var current = await ReadAsync(services, work, draft.ProjectId, created.Value);
        Assert.Equal("Accepted peer assignment", current.Notes);
        Assert.Equal(65m, current.AllocationPercent);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Primary_promotion_checks_only_the_primary_dependency_and_preserves_peer_notes(bool work) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var crm = services.GetRequiredService<ProjectPartyIntegrationService>();
        var first = await PrepareAsync(services, work);
        var second = first.Snapshot();
        second.PartyId = await CreatePersonAsync(services, "Second native participant");
        second.IsPrimary = true;
        first.IsPrimary = true;
        var observed = await crm.ReadPrimaryObservationAsync(first.ExpectedProjectAdmission!, first.Role, first.NodeKey);
        first.PrimaryOriginal = observed;
        second.PrimaryOriginal = observed;
        var created = await SaveAsync(services, work, first);
        Assert.True(created.IsSuccess);
        Assert.Equal(ProjectAssignmentEditingErrors.PrimaryConflict,
            Assert.Single((await SaveAsync(services, work, second)).Errors).Code);

        second.PrimaryOriginal = await crm.ReadPrimaryObservationAsync(first.ExpectedProjectAdmission!, first.Role, first.NodeKey);
        var shown = await ReadAsync(services, work, first.ProjectId, created.Value);
        var peer = From(shown, first.ExpectedProjectAdmission!);
        peer.Notes = "Independent notes while primary is reviewed";
        Assert.True((await SaveAsync(services, work, peer)).IsSuccess);
        Assert.True((await SaveAsync(services, work, second)).IsSuccess);
        var retained = await ReadAsync(services, work, first.ProjectId, created.Value);
        Assert.False(retained.IsPrimary);
        Assert.Equal(peer.Notes, retained.Notes);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_uses_the_shown_original_and_a_deleted_assignment_cannot_be_recreated_by_save(bool work) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var draft = await PrepareAsync(services, work);
        var created = await SaveAsync(services, work, draft);
        Assert.True(created.IsSuccess);
        var shown = await ReadAsync(services, work, draft.ProjectId, created.Value);
        var peer = From(shown, draft.ExpectedProjectAdmission!);
        peer.Notes = "Peer changed before delete";
        Assert.True((await SaveAsync(services, work, peer)).IsSuccess);
        await Assert.ThrowsAsync<ProjectAssignmentConflictException>(() => DeleteAsync(services, work, draft, shown));
        var current = await ReadAsync(services, work, draft.ProjectId, created.Value);
        await DeleteAsync(services, work, draft, current);
        Assert.True((await SaveAsync(services, work, From(current, draft.ExpectedProjectAdmission!))).IsFailure);
        Assert.Empty(await services.GetRequiredService<ProjectPartyIntegrationService>().ListAssignmentsDetailedAsync(draft.ProjectId));
    }

    [Fact]
    public async Task Ownership_transition_uses_the_actual_source_original_and_preserves_identity() {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var draft = await PrepareAsync(services, true);
        var crm = services.GetRequiredService<ProjectPartyIntegrationService>();
        draft.Role = ProjectPartyAssignmentRole.Manager;
        var saved = await crm.SaveAssignmentAsync(draft);
        Assert.True(saved.IsSuccess);
        var shown = await ReadAsync(services, false, draft.ProjectId, saved.Value);
        var transition = From(shown, draft.ExpectedProjectAdmission!);
        transition.Role = ProjectPartyAssignmentRole.WorkItemAssignee;
        Assert.True((await crm.SaveAssignmentAsync(transition)).IsSuccess);
        var work = await services.GetRequiredService<IProjectWorkAssignmentQueries>().GetAsync(saved.Value);
        Assert.NotNull(work);
        Assert.Equal(shown.PhaseName, work.PhaseName);
        Assert.Equal(shown.OpportunityId, work.OpportunityId);
        Assert.True((await crm.SaveAssignmentAsync(transition)).IsFailure);
        var reverse = From(work.Original, draft.ExpectedProjectAdmission!);
        reverse.Role = ProjectPartyAssignmentRole.Manager;
        Assert.True((await crm.SaveAssignmentAsync(reverse)).IsSuccess);
        Assert.Null(await services.GetRequiredService<IProjectWorkAssignmentQueries>().GetAsync(saved.Value));
        Assert.Equal(saved.Value, Assert.Single(await crm.ListAssignmentsDetailedAsync(draft.ProjectId)).Id);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_replacement_cannot_discard_an_unobserved_row_or_a_peers_nonmembership_edit(bool work) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var draft = await PrepareAsync(services, work);
        Assert.True((await SaveAsync(services, work, draft)).IsSuccess);
        var crm = services.GetRequiredService<ProjectPartyIntegrationService>();
        var shown = Assert.Single(await crm.ListAssignmentsDetailedAsync(draft.ProjectId));
        var observed = new[] { ProjectPartyAssignmentConcurrencySnapshot.From(shown) };
        var peer = From(shown.Original!, draft.ExpectedProjectAdmission!);
        peer.Notes = "Peer notes must survive a membership replacement";
        Assert.True((await SaveAsync(services, work, peer)).IsSuccess);

        async Task<Result> ReplaceAsync(IReadOnlyCollection<ProjectPartyAssignmentConcurrencySnapshot>? original) => work
            ? await services.GetRequiredService<IProjectWorkAssignmentCommands>().ReplaceAsync(draft.ProjectId,
                new(draft.NodeKey), [], original, expectedProjectAdmission: draft.ExpectedProjectAdmission)
            : await crm.ReplaceProjectAssignmentsAsync(draft.ProjectId, [], [draft.Role],
                expectedProjectAdmission: draft.ExpectedProjectAdmission, expectedAssignments: original);

        Assert.True((await ReplaceAsync(null)).IsFailure);
        Assert.True((await ReplaceAsync(observed)).IsFailure);
        var current = Assert.Single(await crm.ListAssignmentsDetailedAsync(draft.ProjectId));
        Assert.Equal(peer.Notes, current.Notes);
        Assert.True((await ReplaceAsync([ProjectPartyAssignmentConcurrencySnapshot.From(current)])).IsSuccess);
        Assert.Empty(await crm.ListAssignmentsDetailedAsync(draft.ProjectId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Comparison_read_uses_the_actual_native_owner_and_rejects_another_project_lifetime(bool work) {
        await using var app = await TestApplication.CreateAsync();
        await using var scope = app.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var draft = await PrepareAsync(services, work);
        var saved = await SaveAsync(services, work, draft);
        Assert.True(saved.IsSuccess);
        var owner = services.GetRequiredService<ProjectPartyIntegrationService>();
        var original = await ReadAsync(services, work, draft.ProjectId, saved.Value);
        var project = draft.ExpectedProjectAdmission!;
        Assert.Equal(original, await owner.ReadAssignmentForEditingAsync(project, saved.Value));
        Assert.Null(await owner.ReadAssignmentForEditingAsync(new(project.DatabaseProfileId, project.ProjectId, Guid.NewGuid()), saved.Value));
        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.ReadAssignmentForEditingAsync(
            new(Guid.NewGuid(), project.ProjectId, project.LifetimeId), saved.Value));
        var peer = From(original, project);
        peer.Notes = "Current native comparison content";
        Assert.True((await SaveAsync(services, work, peer)).IsSuccess);
        var current = await owner.ReadAssignmentForEditingAsync(project, saved.Value);
        Assert.NotNull(current);
        Assert.Equal(peer.Notes, current.Notes);
        Assert.Equal(await ReadAsync(services, work, draft.ProjectId, saved.Value), current);
        await DeleteAsync(services, work, peer, current);
        Assert.Null(await owner.ReadAssignmentForEditingAsync(project, saved.Value));
    }

    internal static async Task<ProjectPartyAssignmentUpsertRequest> PrepareAsync(IServiceProvider services, bool work) {
        var project = await StaffingRequestConcurrencyTests.CreateProjectAsync(services);
        var nodeKey = string.Empty;
        if (work) {
            var task = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(project.ProjectId,
                new(ProjectObjectType.WorkItem, "Native observed task", string.Empty, string.Empty, $"project:{project.ProjectId:D}",
                    ObjectSubtype: "task") { ExpectedProjectAdmission = project });
            nodeKey = task.Id;
        }
        return new() {
            ProjectId = project.ProjectId, ExpectedProjectAdmission = project,
            PartyId = await CreatePersonAsync(services, "Native observed participant"),
            Role = work ? ProjectPartyAssignmentRole.WorkItemAssignee : ProjectPartyAssignmentRole.Manager,
            NodeKey = nodeKey, AllocationPercent = 25m, Notes = "Observed native assignment"
        };
    }

    internal static async Task<Guid> CreatePersonAsync(IServiceProvider services, string name) {
        var saved = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            DisplayName = name, PartyType = PartyType.Person
        });
        Assert.True(saved.IsSuccess);
        return saved.Value;
    }

    internal static Task<Result<Guid>> SaveAsync(IServiceProvider services, bool work, ProjectPartyAssignmentUpsertRequest draft) => work
        ? services.GetRequiredService<IProjectWorkAssignmentCommands>().SaveAsync(draft)
        : services.GetRequiredService<ProjectPartyIntegrationService>().SaveAssignmentAsync(draft);

    internal static async Task<ProjectAssignmentObservation> ReadAsync(IServiceProvider services, bool work, Guid projectId, Guid id) => work
        ? (await services.GetRequiredService<IProjectWorkAssignmentQueries>().GetAsync(id))!.Original
        : Assert.Single(await services.GetRequiredService<ProjectPartyIntegrationService>().ListAssignmentsDetailedAsync(projectId), row => row.Id == id).Original!;

    internal static ProjectPartyAssignmentUpsertRequest From(ProjectAssignmentObservation row, ProjectWriteAdmission project) => new() {
        AssignmentId = row.Id, Original = row, ProjectId = row.ProjectId, ExpectedProjectAdmission = project,
        PartyId = row.PartyId, PartyAffiliationId = row.PartyAffiliationId, Role = row.Role, NodeKey = row.NodeKey,
        IsPrimary = row.IsPrimary, AllocationPercent = row.AllocationPercent,
        StartsOn = row.StartsAtUtc is { } start ? DateOnly.FromDateTime(start.UtcDateTime) : null,
        EndsOn = row.EndsAtUtc is { } end ? DateOnly.FromDateTime(end.UtcDateTime) : null, Source = row.Source, Notes = row.Notes
    };

    private static Task DeleteAsync(IServiceProvider services, bool work, ProjectPartyAssignmentUpsertRequest draft, ProjectAssignmentObservation original) => work
        ? services.GetRequiredService<IProjectWorkAssignmentCommands>().DeleteAsync(original.Id,
            expectedReference: ProjectAssignmentReference.From(draft.ExpectedProjectAdmission!), original: original)
        : services.GetRequiredService<ProjectPartyIntegrationService>().DeleteAssignmentAsync(original.Id,
            expectedReference: ProjectAssignmentReference.From(draft.ExpectedProjectAdmission!), original: original);
}
