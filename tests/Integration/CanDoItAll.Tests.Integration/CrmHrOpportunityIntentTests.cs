using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.CrmHr;

public sealed class CrmHrOpportunityIntentTests {
    [Fact]
    public async Task Modal_and_board_writes_use_their_own_displayed_observations() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<CrmService>();
        var editor = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        editor.Notes = "Modal saved before the board click";
        Assert.True((await owner.SaveOpportunityAsync(editor)).IsSuccess);
        var stale = await owner.AdvanceOpportunityStageAsync(Intent(original), "board-test");
        Assert.True(stale.IsFailure);
        Assert.Contains(stale.Errors, error => error.Code == CrmHrEditingErrorCodes.OpportunityConflict);
        var before = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityAsync(original.Id));
        Assert.Equal(original.Stage, before.Stage);

        var advanced = await owner.AdvanceOpportunityStageAsync(Intent(before), "board-test");
        Assert.True(advanced.IsSuccess);
        var outdatedModal = CrmHrOpportunityConcurrentEditingTests.Edit(before);
        outdatedModal.Title = "Must not overwrite the board stage";
        Assert.True((await owner.SaveOpportunityAsync(outdatedModal)).IsFailure);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityAsync(original.Id));
        Assert.Equal(OpportunityStage.Qualified, current.Stage);
        Assert.Equal(before.Title, current.Title);
        Assert.Equal(editor.Notes, current.Notes);
        Assert.Equal(2, current.StageHistory.Count);
        Assert.Equal(advanced.Value!.UpdatedAtUtc, current.UpdatedAtUtc);
    }

    [Fact]
    public async Task Linked_party_rows_keep_their_identity_when_updated_and_cannot_borrow_a_foreign_identity() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<CrmService>();
        var editor = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        editor.Parties.Add(new() { PartyId = original.OwnerPartyId, Role = OpportunityPartyRole.Partner });
        Assert.True((await owner.SaveOpportunityAsync(editor)).IsSuccess);
        var saved = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityAsync(original.Id));
        editor = CrmHrOpportunityConcurrentEditingTests.Edit(saved);
        editor.Parties[0].Role = OpportunityPartyRole.Sponsor;
        Assert.True((await owner.SaveOpportunityAsync(editor)).IsSuccess);
        var updated = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityAsync(original.Id));
        Assert.Equal(Assert.Single(saved.Parties).Id, Assert.Single(updated.Parties).Id);
        Assert.Equal(OpportunityPartyRole.Sponsor, updated.Parties[0].Role);
        var another = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var foreign = CrmHrOpportunityConcurrentEditingTests.Edit(another);
        foreign.Parties.Add(new() { Id = saved.Parties[0].Id, PartyId = original.OwnerPartyId });
        var refused = await owner.SaveOpportunityAsync(foreign);
        Assert.True(refused.IsFailure);
        Assert.Contains(refused.Errors, error => error.Code == CrmHrEditingErrorCodes.OpportunityChildIdentityInvalid);
    }

    [Fact]
    public async Task A_stage_note_without_a_transition_is_refused_without_an_extra_history_entry() {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<CrmService>();
        var editor = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        editor.StageNotes = "This must never be silently discarded";
        var result = await owner.SaveOpportunityAsync(editor);
        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.OpportunityStageNoteWithoutTransition);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityAsync(original.Id));
        Assert.Equal(original.UpdatedAtUtc, current.UpdatedAtUtc);
        Assert.Equal(original.StageHistory.Count, current.StageHistory.Count);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(0, true)]
    [InlineData(1, false)]
    [InlineData(1, true)]
    [InlineData(2, false)]
    [InlineData(2, true)]
    public async Task Party_type_changes_and_imports_cannot_invalidate_opportunity_references(int reference, bool import) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var directory = scope.ServiceProvider.GetRequiredService<PartyDirectoryService>();
        var unit = await directory.SavePartyAsync(new() { PartyType = PartyType.OrganizationUnit, DisplayName = "Opportunity delivery unit" });
        Assert.True(unit.IsSuccess);
        var editor = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        editor.DeliveryUnitPartyId = unit.Value;
        Assert.True((await scope.ServiceProvider.GetRequiredService<CrmService>().SaveOpportunityAsync(editor)).IsSuccess);
        var id = reference switch { 0 => original.AccountPartyId, 1 => original.OwnerPartyId, _ => unit.Value };
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<CrmHrDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        var party = await context.Set<Party>().AsNoTracking().SingleAsync(row => row.Id == id);
        var changed = new PartyEditorModel { Id = id, UpdatedAtUtc = party.UpdatedAtUtc, DisplayName = party.DisplayName,
            PartyType = reference == 0 ? PartyType.Person : PartyType.Organization };

        Result result = import ? await directory.ImportPartiesAtomicallyAsync([changed], "type-test") : await directory.SavePartyAsync(changed);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.OpportunityPartyTypeConflict);
        Assert.Equal(party.PartyType, await context.Set<Party>().AsNoTracking().Where(row => row.Id == id).Select(row => row.PartyType).SingleAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task A_linked_project_write_requires_the_observed_database_and_lifetime(bool replacement) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var original = await CrmHrOpportunityConcurrentEditingTests.SeedAsync(scope.ServiceProvider);
        var owner = scope.ServiceProvider.GetRequiredService<CrmService>();
        var project = new Project { Name = "Observed opportunity project", Slug = Guid.NewGuid().ToString("N") };
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var context = await factory.CreateDbContextAsync()) {
            context.Set<Project>().Add(project);
            await context.SaveChangesAsync();
        }
        var admission = Assert.IsType<ProjectWriteAdmission>(await scope.ServiceProvider.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(project.Id));
        var editor = CrmHrOpportunityConcurrentEditingTests.Edit(original);
        editor.LinkedProjectId = project.Id;
        editor.LinkedProjectAdmission = admission;
        Assert.True((await owner.SaveOpportunityAsync(editor)).IsSuccess);
        var saved = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityAsync(original.Id));
        Assert.Equal(admission, saved.LinkedProjectAdmission);
        editor = CrmHrOpportunityConcurrentEditingTests.Edit(saved);
        editor.Notes = "Must not bind an old draft to another project authority";
        if (replacement) {
            await using var context = await factory.CreateDbContextAsync();
            var active = await context.Set<Project>().SingleAsync(row => row.Id == project.Id);
            context.Set<ProjectRetirementRecord>().Add(new() { ProjectId = project.Id, LifetimeId = admission.LifetimeId, RetiredAtUtc = DateTimeOffset.UtcNow });
            context.Entry(active).Property(row => row.LifetimeId).CurrentValue = Guid.NewGuid();
            await context.SaveChangesAsync();
        } else {
            editor.LinkedProjectAdmission = new(Guid.NewGuid(), admission.ProjectId, admission.LifetimeId);
        }

        var result = await owner.SaveOpportunityAsync(editor);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == CrmHrEditingErrorCodes.OpportunityProjectStale);
        var current = Assert.IsType<CrmOpportunityDetailModel>(await owner.GetOpportunityAsync(original.Id));
        Assert.Equal(saved.UpdatedAtUtc, current.UpdatedAtUtc);
        Assert.Equal(saved.Notes, current.Notes);
        if (replacement) {
            Assert.NotEqual(admission, current.LinkedProjectAdmission);
        }
    }

    private static CrmOpportunityStageAdvance Intent(CrmOpportunityDetailModel value) => new(value.Id, value.AccountPartyId, value.Stage, value.UpdatedAtUtc);
}
