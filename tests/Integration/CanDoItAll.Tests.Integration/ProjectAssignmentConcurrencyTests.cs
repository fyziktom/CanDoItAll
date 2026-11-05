using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class ProjectAssignmentConcurrencyTests {
    [Fact]
    public async Task Creating_a_duplicate_assignment_cannot_silently_replace_an_existing_composite_match() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var (owner, draft) = await PrepareAsync(scope.ServiceProvider);
        var created = await owner.SaveAssignmentAsync(draft);
        Assert.True(created.IsSuccess);
        var duplicate = draft.Snapshot();
        duplicate.AllocationPercent = 95m;
        duplicate.Notes = "Unobserved composite replacement";

        var result = await owner.SaveAssignmentAsync(duplicate);

        Assert.True(result.IsFailure);
        var current = Assert.Single(await owner.ListAssignmentsDetailedAsync(draft.ProjectId));
        Assert.Equal(created.Value, current.Id);
        Assert.Equal(25m, current.AllocationPercent);
        Assert.Equal("Original participation", current.Notes);
    }

    [Fact]
    public async Task A_stale_assignment_cannot_replace_a_peers_saved_participation() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var (owner, draft) = await PrepareAsync(scope.ServiceProvider);
        var created = await owner.SaveAssignmentAsync(draft);
        Assert.True(created.IsSuccess);
        var shown = Assert.Single(await owner.ListAssignmentsDetailedAsync(draft.ProjectId));
        var peer = From(shown, draft.ExpectedProjectAdmission!);
        peer.AllocationPercent = 70m;
        peer.Notes = "Accepted peer participation";
        Assert.True((await owner.SaveAssignmentAsync(peer)).IsSuccess);
        var stale = From(shown, draft.ExpectedProjectAdmission!);
        stale.Notes = "Stale assignment replacement";

        var result = await owner.SaveAssignmentAsync(stale);

        Assert.True(result.IsFailure);
        var current = Assert.Single(await owner.ListAssignmentsDetailedAsync(draft.ProjectId));
        Assert.Equal("Accepted peer participation", current.Notes);
        Assert.Equal(70m, current.AllocationPercent);
    }

    [Fact]
    public async Task A_project_lifetime_reference_alone_cannot_authorize_a_row_delete() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var (owner, draft) = await PrepareAsync(scope.ServiceProvider);
        var created = await owner.SaveAssignmentAsync(draft);
        Assert.True(created.IsSuccess);

        await Assert.ThrowsAsync<InvalidOperationException>(() => owner.DeleteAssignmentAsync(created.Value,
            expectedReference: ProjectAssignmentReference.From(draft.ExpectedProjectAdmission!)));

        Assert.Equal(created.Value, Assert.Single(await owner.ListAssignmentsDetailedAsync(draft.ProjectId)).Id);
    }

    private static async Task<(ProjectPartyIntegrationService Owner, ProjectPartyAssignmentUpsertRequest Draft)> PrepareAsync(IServiceProvider services) {
        var project = await services.GetRequiredService<ProjectsService>().CreateWithAdmissionAsync(new() {
            Name = "Observed participation project", Description = "Native assignment concurrency proof"
        });
        Assert.True(project.IsSuccess);
        var party = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            DisplayName = "Native assignment participant", PartyType = PartyType.Person
        });
        Assert.True(party.IsSuccess);
        return (services.GetRequiredService<ProjectPartyIntegrationService>(), new() {
            ProjectId = project.Value!.ProjectId, ExpectedProjectAdmission = project.Value,
            PartyId = party.Value, Role = ProjectPartyAssignmentRole.Manager, AllocationPercent = 25m,
            Notes = "Original participation"
        });
    }

    private static ProjectPartyAssignmentUpsertRequest From(ProjectPartyAssignmentDetail row, ProjectWriteAdmission project) => new() {
        AssignmentId = row.Id, Original = row.Original, ProjectId = row.ProjectId, ExpectedProjectAdmission = project, PartyId = row.PartyId,
        PartyAffiliationId = row.PartyAffiliationId, Role = row.Role, NodeKey = row.NodeKey, IsPrimary = row.IsPrimary,
        AllocationPercent = row.AllocationPercent, StartsOn = row.StartsAtUtc is { } start ? DateOnly.FromDateTime(start.UtcDateTime) : null,
        EndsOn = row.EndsAtUtc is { } end ? DateOnly.FromDateTime(end.UtcDateTime) : null, Source = row.Source, Notes = row.Notes
    };
}
