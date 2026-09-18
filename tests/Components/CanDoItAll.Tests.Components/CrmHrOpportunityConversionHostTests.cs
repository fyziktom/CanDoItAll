using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Converting an opportunity that is already linked to a project, through the CRM host and the real owners. The
// preselected project carries the admission captured by the load that showed it; a missing or stale admission is a
// visible rejection that writes nothing and keeps the dialog open, never an unhandled circuit failure.
public sealed class CrmHrOpportunityConversionHostTests
{
    [Fact]
    public async Task A_linked_project_is_preselected_with_the_admission_its_load_captured_and_relinking_creates_no_project()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var seed = await SeedConvertedOpportunityAsync(harness, "Relink");
        var cut = RenderOpportunity(harness, seed);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        var admissions = harness.Context.Services.GetRequiredService<ProjectWriteAdmissionService>();
        var captured = await admissions.CaptureAsync(seed.ProjectId);
        Assert.NotNull(captured);

        await cut.InvokeAsync(view.OpenConversionDialogAsync);

        Assert.True(view.IsOpportunityConversionDialogOpen);
        Assert.True(view.OpportunityConversionEditor.LinkExistingProject);
        Assert.Equal(seed.ProjectId, view.OpportunityConversionEditor.ExistingProjectId);
        Assert.Equal(captured, view.OpportunityConversionEditor.ExpectedProjectAdmission);

        await cut.InvokeAsync(() => view.SaveOpportunityConversionAsync(view.OpportunityConversionEditor));

        Assert.False(view.IsOpportunityConversionDialogOpen);
        Assert.True(view.IsOpportunityDetailDialogOpen);
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        Assert.Equal(seed.ProjectId, (await crm.GetOpportunityAsync(seed.OpportunityId))!.LinkedProjectId);
        Assert.Equal(1, await CountProjectsAsync(harness, seed.ProjectName));
    }

    [Fact]
    public async Task A_stale_project_lifetime_is_rejected_visibly_and_writes_nothing()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var seed = await SeedConvertedOpportunityAsync(harness, "Stale lifetime");
        var cut = RenderOpportunity(harness, seed);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var before = await crm.GetOpportunityAsync(seed.OpportunityId);
        await cut.InvokeAsync(view.OpenConversionDialogAsync);
        var captured = view.OpportunityConversionEditor.ExpectedProjectAdmission!;
        var submission = view.OpportunityConversionEditor.Snapshot();
        // The project was retired and recreated under the same public identity after the dialog captured it.
        submission.ExpectedProjectAdmission = new ProjectWriteAdmission(captured.DatabaseProfileId, captured.ProjectId, Guid.NewGuid());

        await cut.InvokeAsync(() => view.SaveOpportunityConversionAsync(submission));

        Assert.True(view.IsOpportunityConversionDialogOpen);
        Assert.False(view.IsOpportunityConversionBusy);
        Assert.Contains("changed after it was chosen", view.Message, StringComparison.Ordinal);
        var after = await crm.GetOpportunityAsync(seed.OpportunityId);
        Assert.Equal(before!.UpdatedAtUtc, after!.UpdatedAtUtc);
        Assert.Equal(1, await CountProjectsAsync(harness, seed.ProjectName));
    }

    [Fact]
    public async Task A_linked_project_without_a_captured_admission_is_rejected_before_the_owner_is_called()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var seed = await SeedConvertedOpportunityAsync(harness, "Missing admission");
        var cut = RenderOpportunity(harness, seed);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var before = await crm.GetOpportunityAsync(seed.OpportunityId);
        await cut.InvokeAsync(view.OpenConversionDialogAsync);
        var submission = view.OpportunityConversionEditor.Snapshot();
        submission.ExpectedProjectAdmission = null;

        await cut.InvokeAsync(() => view.SaveOpportunityConversionAsync(submission));

        Assert.True(view.IsOpportunityConversionDialogOpen);
        Assert.Contains("Choose the project to link", view.Message, StringComparison.Ordinal);
        var after = await crm.GetOpportunityAsync(seed.OpportunityId);
        Assert.Equal(before!.UpdatedAtUtc, after!.UpdatedAtUtc);
        Assert.Equal(1, await CountProjectsAsync(harness, seed.ProjectName));
    }

    private static IRenderedComponent<CrmHrCrmPage> RenderOpportunity(ComponentTestHarness harness, ConvertedOpportunity seed)
    {
        harness.Context.Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/crm-hr/crm?accountId={seed.AccountId:D}&opportunityId={seed.OpportunityId:D}");
        var cut = harness.Context.Render<CrmHrCrmPage>();
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(seed.OpportunityId, view.SelectedOpportunityId);
            Assert.True(view.IsOpportunityDetailDialogOpen);
        });
        return cut;
    }

    private static async Task<int> CountProjectsAsync(ComponentTestHarness harness, string projectName)
    {
        var page = await harness.Context.Services.GetRequiredService<IProjectRecordQueryService>()
            .SearchAsync(new ProjectRecordQuery(projectName));
        return page.Items.Count(item => string.Equals(item.Name, projectName, StringComparison.Ordinal));
    }

    private static async Task<ConvertedOpportunity> SeedConvertedOpportunityAsync(ComponentTestHarness harness, string label)
    {
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var accountId = await CreatePartyAsync(parties, $"Conversion account {label} {suffix}", PartyType.Organization, PartyRoleKind.Customer);
        var ownerId = await CreatePartyAsync(parties, $"Conversion owner {label} {suffix}", PartyType.Person, PartyRoleKind.AccountManager);
        var saved = await crm.SaveOpportunityAsync(new CrmOpportunityEditorModel
        {
            AccountPartyId = accountId,
            Title = $"Conversion opportunity {label} {suffix}",
            Stage = OpportunityStage.Won,
            OpportunitySource = OpportunitySource.Direct,
            OwnerPartyId = ownerId,
            CurrencyCode = "USD",
            Amount = 42000m,
            ProbabilityPercent = 100,
            LastChangedBy = "component-tests"
        });
        Assert.True(saved.IsSuccess);
        var opportunity = await crm.GetOpportunityAsync(saved.Value);
        var projectName = $"Conversion project {label} {suffix}";
        var converted = await crm.ConvertOpportunityToProjectAsync(new CrmOpportunityConversionEditorModel
        {
            OpportunityId = saved.Value,
            ExpectedUpdatedAtUtc = opportunity!.UpdatedAtUtc,
            ProjectName = projectName,
            CurrentPhase = "Sales handoff",
            LastChangedBy = "component-tests"
        });
        Assert.True(converted.IsSuccess);
        Assert.True(converted.Value!.CreatedNewProject);
        return new ConvertedOpportunity(accountId, saved.Value, converted.Value.ProjectId, projectName);
    }

    private static async Task<Guid> CreatePartyAsync(PartyDirectoryService parties, string displayName, PartyType partyType, PartyRoleKind roleKind)
    {
        var result = await parties.SavePartyAsync(new PartyEditorModel
        {
            PartyType = partyType,
            LifecycleStatus = PartyLifecycleStatus.Active,
            DisplayName = displayName,
            Summary = $"{displayName} summary",
            LastChangedBy = "component-tests",
            Roles = [new PartyRoleAssignmentEditorModel { RoleKind = roleKind, Title = roleKind.ToString(), IsPrimary = true }]
        });
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private sealed record ConvertedOpportunity(Guid AccountId, Guid OpportunityId, Guid ProjectId, string ProjectName);
}
