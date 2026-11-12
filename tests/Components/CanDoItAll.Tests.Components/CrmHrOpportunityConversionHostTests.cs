using Bunit;
using CanDoItAll.CrmHr.UI.Crm;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Projects;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Components.CrmHr;

// Converting an opportunity that is already linked to a project, through the CRM host and the real owners. The
// preselected project carries the admission captured by the load that showed it; a missing or stale admission is a
// visible rejection that writes nothing and keeps the dialog open, never an unhandled circuit failure.
public sealed class CrmHrOpportunityConversionHostTests
{
    [Fact]
    public async Task A_native_conversion_keeps_unblurred_later_input_and_its_form_until_explicit_discard() {
        var gate = new ConversionResponseGate();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.RemoveAll<ICrmOpportunityConversionService>();
            services.AddScoped<ICrmOpportunityConversionService>(provider => new HeldConversionOwner(provider.GetRequiredService<CrmService>(), gate));
        });
        var seed = await SeedConvertedOpportunityAsync(harness, "Later raw input");
        var cut = RenderOpportunity(harness, seed);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        await cut.InvokeAsync(view.OpenConversionDialogAsync);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-conversion-link-existing']").ChangeAsync(new ChangeEventArgs { Value = false }));
        var name = seed.ProjectName + " retained later draft";
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-conversion-name']").InputAsync(new ChangeEventArgs { Value = name }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-conversion-description']").InputAsync(new ChangeEventArgs { Value = "Submitted project description" }));
        var model = view.OpportunityConversionEditor;
        var context = ComponentTestHarness.FindEditContext(cut, model);
        var saving = cut.InvokeAsync(() => view.SaveOpportunityConversionAsync(model));
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-conversion-description']").InputAsync(new ChangeEventArgs { Value = "  Later unblurred target description  " }));
        } finally {
            gate.Release.TrySetResult();
        }
        await saving;

        Assert.True(view.IsOpportunityConversionDialogOpen);
        Assert.Same(model, view.OpportunityConversionEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, model));
        Assert.Equal("  Later unblurred target description  ", model.ProjectDescription);
        Assert.True(cut.Find("[data-testid='crmhr-opportunity-conversion-save']").HasAttribute("disabled"));
        var accepted = Assert.IsType<CrmOpportunityConversionResult>(gate.Accepted);
        var project = await harness.Context.Services.GetRequiredService<ProjectsService>().GetAsync(accepted.ProjectId);
        Assert.Equal("Submitted project description", project.Description);
        await cut.InvokeAsync(() => view.SaveOpportunityConversionAsync(model));
        Assert.Equal(1, gate.Writes);
        var closing = cut.InvokeAsync(view.CloseOpportunityConversionDialogAsync);
        cut.WaitForElement("[data-testid='crmhr-conversion-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-conversion-dirty-discard']").ClickAsync(new()));
        await closing;
        Assert.Equal(accepted.ProjectId, view.SelectedOpportunity!.LinkedProjectId);
        Assert.False(view.IsOpportunityConversionDialogOpen);
        Assert.Equal(1, await CountProjectsAsync(harness, name));
    }

    [Fact]
    public async Task A_peer_source_edit_requires_local_review_without_replacing_the_conversion_draft() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var seed = await SeedConvertedOpportunityAsync(harness, "Source review");
        var cut = RenderOpportunity(harness, seed);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        await cut.InvokeAsync(view.OpenConversionDialogAsync);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-conversion-link-existing']").ChangeAsync(new ChangeEventArgs { Value = false }));
        var model = view.OpportunityConversionEditor;
        model.ProjectName = seed.ProjectName + " explicit new target";
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-opportunity-conversion-description']").InputAsync(new ChangeEventArgs { Value = "  Retained raw request  " }));
        var context = ComponentTestHarness.FindEditContext(cut, model);
        var crm = harness.Context.Services.GetRequiredService<CrmService>();
        var peer = OpportunityEditorDrafts.FromDetail((await crm.GetOpportunityAsync(seed.OpportunityId))!);
        peer.Summary = "Current peer conversion source";
        Assert.True((await crm.SaveOpportunityAsync(peer)).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-conversion-freshness']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-conversion-review-open']").ClickAsync(new()));
        cut.WaitForElement("[data-testid='crmhr-conversion-review-dialog']");
        Assert.Contains("Current peer conversion source", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("conversionEditing.Message", cut.Markup, StringComparison.Ordinal);
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-conversion-review-apply']").ClickAsync(new()));

        Assert.Same(model, view.OpportunityConversionEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, model));
        Assert.Equal("  Retained raw request  ", model.ProjectDescription);
        Assert.Equal((await crm.GetOpportunityAsync(seed.OpportunityId))!.UpdatedAtUtc, model.ExpectedUpdatedAtUtc);
        Assert.Equal(0, await CountProjectsAsync(harness, model.ProjectName));
    }

    [Fact]
    public async Task A_known_conversion_with_failed_read_back_retires_the_submit_action_and_does_not_create_twice() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(
            services => control = CrmHrOwnerReadBackControl.Install(services));
        var seed = await SeedConvertedOpportunityAsync(harness, "Known conversion read-back");
        var cut = RenderOpportunity(harness, seed);
        var view = (ICrmHrCrmWorkspaceView)cut.Instance;
        await cut.InvokeAsync(view.OpenConversionDialogAsync);
        var submitted = view.OpportunityConversionEditor;
        submitted.LinkExistingProject = false;
        submitted.ProjectName = seed.ProjectName + " second target";
        control.FailReadIn(nameof(CrmService.GetAccountWorkspaceAsync), "SaveOpportunityConversionAsync");

        await cut.InvokeAsync(() => view.SaveOpportunityConversionAsync(submitted));

        Assert.Equal(1, control.Interceptions);
        Assert.Equal(1, await CountProjectsAsync(harness, submitted.ProjectName));
        Assert.False(view.IsOpportunityConversionDialogOpen);
        Assert.True(view.IsOpportunityDetailDialogOpen);
        await cut.InvokeAsync(() => view.SaveOpportunityConversionAsync(submitted));
        Assert.Equal(1, await CountProjectsAsync(harness, submitted.ProjectName));
    }

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

    private sealed class ConversionResponseGate {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CrmOpportunityConversionResult? Accepted { get; set; }
        public int Writes { get; set; }
    }

    private sealed class HeldConversionOwner(CrmService owner, ConversionResponseGate gate) : ICrmOpportunityConversionService {
        public Task<CrmOpportunityDetailModel?> GetOpportunityForEditingAsync(Guid opportunityId, CancellationToken cancellationToken = default, int? maximumPartyLinks = null) =>
            owner.GetOpportunityForEditingAsync(opportunityId, cancellationToken, maximumPartyLinks);
        public Task<Result<CrmOpportunityConversionProjectBasis>> ObserveOpportunityProjectBasisAsync(Guid opportunityId,
            DateTimeOffset expectedOpportunityUpdatedAtUtc, ProjectWriteAdmission project, CancellationToken cancellationToken = default) =>
            owner.ObserveOpportunityProjectBasisAsync(opportunityId, expectedOpportunityUpdatedAtUtc, project, cancellationToken);
        public async Task<Result<CrmOpportunityConversionResult>> ConvertOpportunityToProjectAsync(CrmOpportunityConversionEditorModel model,
            CancellationToken cancellationToken = default, Action<CrmOpportunityConversionResult>? onCommitted = null) {
            gate.Writes++;
            var result = await owner.ConvertOpportunityToProjectAsync(model, cancellationToken, receipt => {
                gate.Accepted = receipt;
                onCommitted?.Invoke(receipt);
            });
            gate.Entered.TrySetResult();
            await gate.Release.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            return result;
        }
    }
}
