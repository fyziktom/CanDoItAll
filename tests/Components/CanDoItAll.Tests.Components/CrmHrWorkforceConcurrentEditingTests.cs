using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Workforce;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrWorkforceConcurrentEditingTests {
    [Fact]
    public async Task A_peer_profile_commit_refreshes_the_catalog_without_replacing_the_open_profile() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, party) = await OpenAsync(harness);
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var draft = view.ProfileEditor;
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        var profiles = harness.Context.Services.GetRequiredService<IWorkforceProfileEditing>();
        var peer = (await profiles.GetWorkforceProfileWorkspaceAsync(party))!.Profile;
        peer.JobTitle = "Catalog peer workforce title";

        Assert.True((await profiles.SaveWorkforceProfileAsync(peer)).IsSuccess);

        cut.WaitForAssertion(() => Assert.Contains("Catalog peer workforce title",
            cut.Find("[data-testid='crmhr-workforce-item']").TextContent, StringComparison.Ordinal));
        Assert.Same(draft, view.ProfileEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Equal("Original title", draft.JobTitle);
    }

    [Fact]
    public async Task A_clean_native_profile_stays_visible_until_explicit_refresh_and_keeps_its_form_context() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, party) = await OpenAsync(harness);
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var draft = view.ProfileEditor;
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        var profiles = harness.Context.Services.GetRequiredService<IWorkforceProfileEditing>();
        var peer = (await profiles.GetWorkforceProfileWorkspaceAsync(party))!.Profile;
        peer.JobTitle = "Peer title needing explicit pull";

        Assert.True((await profiles.SaveWorkforceProfileAsync(peer)).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-workforce-profile-outcome']");
        Assert.Equal("Original title", draft.JobTitle);
        Assert.True(cut.Find("[data-testid='crmhr-workforce-save-button']").HasAttribute("disabled"));
        await cut.Find("[data-testid='crmhr-workforce-refresh-records']").ClickAsync(new());

        Assert.Same(draft, view.ProfileEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Equal("Peer title needing explicit pull", draft.JobTitle);
        Assert.False(cut.Find("[data-testid='crmhr-workforce-save-button']").HasAttribute("disabled"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Completing_a_date_with_its_unchanged_value_does_not_leave_a_valid_form_blocked(bool capacity) {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, _) = await OpenAsync(harness);
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        if (capacity) {
            await cut.Find("[data-testid='crmhr-workforce-tab-allocations']").ClickAsync(new());
        }
        var field = capacity ? "crmhr-capacity-block-start-date" : "crmhr-workforce-start-date";
        var save = capacity ? "crmhr-capacity-block-save-button" : "crmhr-workforce-save-button";
        var value = (capacity ? view.CapacityBlockEditor.StartDate : view.ProfileEditor.StartDate)!.Value.ToString("yyyy-MM-dd");

        await cut.WaitForElement($"[data-testid='{field}']").TriggerEventAsync("oncadnativeinput", new NativeInputEventArgs { Value = value });
        await cut.Find($"[data-testid='{field}']").ChangeAsync(new() { Value = value });

        Assert.False(cut.Find($"[data-testid='{save}']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task A_keyed_skill_review_preserves_unblurred_input_and_requires_a_separate_native_save() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, party) = await OpenAsync(harness);
        var hr = harness.Context.Services.GetRequiredService<HrService>();
        var definition = await hr.SaveSkillDefinitionAsync(new() { Name = "Native review skill" });
        Assert.True(definition.IsSuccess);
        await cut.WaitForElement("[data-testid='crmhr-workforce-refresh-records']").ClickAsync(new());
        await cut.Find("[data-testid='crmhr-workforce-tab-skills']").ClickAsync(new());
        await cut.Find("[data-testid='crmhr-skill-skill-id']").ChangeAsync(new() { Value = definition.Value.ToString() });
        await cut.Find("[data-testid='crmhr-skill-notes']").InputAsync(new() { Value = "Original skill notes" });
        await cut.Find("[data-testid='crmhr-skill-form']").SubmitAsync();
        await cut.Find("[data-testid='crmhr-skill-skill-id']").ChangeAsync(new() { Value = definition.Value.ToString() });
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var draft = view.SkillEditor;
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        await cut.Find("[data-testid='crmhr-skill-notes']").InputAsync(new() { Value = "  Local unblurred skill notes  " });
        var peer = Assert.Single((await hr.GetWorkforceProfileWorkspaceAsync(party))!.Skills);
        Assert.True((await hr.SavePartySkillAsync(new() {
            Id = peer.Id, PartyId = party, SkillId = peer.SkillId, Original = peer.Original, Proficiency = peer.Proficiency,
            YearsExperience = 6, CertificationStatus = peer.CertificationStatus, LastValidatedOn = peer.LastValidatedOn, Notes = peer.Notes
        })).IsSuccess);
        await cut.WaitForElement("[data-testid='crmhr-workforce-skill-review-open']").ClickAsync(new());
        await cut.WaitForElement("[data-testid='crmhr-workforce-skill-review-apply']").ClickAsync(new());

        Assert.Same(draft, view.SkillEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Equal(6, draft.YearsExperience);
        Assert.Equal("  Local unblurred skill notes  ", draft.Notes);
        Assert.Equal("Original skill notes", Assert.Single((await hr.GetWorkforceProfileWorkspaceAsync(party))!.Skills).Notes);
        await cut.Find("[data-testid='crmhr-skill-form']").SubmitAsync();
        Assert.Equal("Local unblurred skill notes", Assert.Single((await hr.GetWorkforceProfileWorkspaceAsync(party))!.Skills).Notes);
    }

    [Fact]
    public async Task A_capacity_acceptance_and_refresh_keep_later_raw_notes_and_invalid_date_in_one_form() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        var (cut, party) = await OpenAsync(harness);
        await cut.Find("[data-testid='crmhr-workforce-tab-allocations']").ClickAsync(new());
        cut.WaitForElement("[data-testid='crmhr-capacity-form']");
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var draft = view.CapacityBlockEditor;
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        await cut.Find("[data-testid='crmhr-capacity-block-notes']").InputAsync(new() { Value = "Submitted capacity notes" });
        control.HoldWriteIn(nameof(WorkforceCapacityEditingService.SaveCapacityBlockAsync));
        var saving = cut.Find("[data-testid='crmhr-capacity-form']").SubmitAsync();
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-capacity-block-notes']").InputAsync(new() { Value = "  Later capacity notes  " }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-capacity-block-start-date']").TriggerEventAsync("oncadnativeinput", new NativeInputEventArgs { Value = "not-a-date", BadInput = true }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-capacity-block-start-date']").ChangeAsync(new() { Value = "not-a-date" }));
        control.Release();
        await saving.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.Find("[data-testid='crmhr-workforce-refresh-records']").ClickAsync(new());

        Assert.Same(draft, view.CapacityBlockEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Equal("  Later capacity notes  ", draft.Notes);
        Assert.Null(draft.Id);
        Assert.Contains(context.GetValidationMessages(), message => message.Contains("date", StringComparison.OrdinalIgnoreCase));
        Assert.True(cut.Find("[data-testid='crmhr-capacity-block-save-button']").HasAttribute("disabled"));
        Assert.Equal("Submitted capacity notes", Assert.Single((await harness.Context.Services.GetRequiredService<HrService>().GetWorkforceCapacityWorkspaceAsync(party))!.CapacityBlocks).Notes);
    }

    [Fact]
    public async Task A_catalog_create_keeps_later_unblurred_input_in_its_original_form() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        var (cut, _) = await OpenAsync(harness);
        await cut.Find("[data-testid='crmhr-workforce-tab-skills']").ClickAsync(new());
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var draft = view.SkillDefinitionEditor;
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        await cut.Find("[data-testid='crmhr-skill-definition-name']").InputAsync(new() { Value = "Submitted catalog skill" });
        control.HoldWriteIn(nameof(WorkforceSkillEditingService.SaveSkillDefinitionAsync));
        var saving = cut.Find("[data-testid='crmhr-skill-definition-form']").SubmitAsync();
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-skill-definition-name']").InputAsync(new() { Value = "  Next catalog skill  " }));
        control.Release();
        await saving.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Same(draft, view.SkillDefinitionEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Equal("  Next catalog skill  ", draft.Name);
        Assert.Null(draft.Id);
        Assert.Equal("Submitted catalog skill", Assert.Single(await harness.Context.Services.GetRequiredService<HrService>().ListSkillCatalogAsync()).Name);
    }

    [Fact]
    public async Task Delivery_unit_creation_keeps_later_input_over_the_accepted_party_and_saves_it_as_an_update() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        var (cut, _) = await OpenAsync(harness);
        await cut.Find("[data-testid='crmhr-workforce-record-close']").ClickAsync(new());
        await cut.Find("[data-testid='crmhr-workforce-new-delivery-unit']").ClickAsync(new());
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var draft = view.DeliveryUnitEditor;
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        await cut.Find("[data-testid='crmhr-delivery-unit-name']").InputAsync(new() { Value = "  Submitted delivery unit  " });
        control.HoldWriteIn(nameof(PartyDirectoryService.SavePartyWithReceiptAsync));
        var saving = cut.Find("[data-testid='crmhr-delivery-unit-form']").SubmitAsync();
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-delivery-unit-summary']").InputAsync(new() { Value = "  Later unit summary  " }));
        control.Release();
        await saving.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.True(view.IsDeliveryUnitDialogOpen);
        Assert.Same(draft, view.DeliveryUnitEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.NotNull(draft.Id);
        Assert.Equal("  Later unit summary  ", draft.Summary);
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        Assert.Empty((await parties.GetPartyAsync(draft.Id.Value))!.Summary);
        await cut.Find("[data-testid='crmhr-delivery-unit-form']").SubmitAsync();
        Assert.Equal("Later unit summary", (await parties.GetPartyAsync(draft.Id.Value))!.Summary);
        Assert.Equal(draft.Id, view.SelectedWorkspace?.PartyId);
    }

    [Fact]
    public async Task Native_profile_review_keeps_unblurred_notes_and_one_context_until_a_separate_save() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, party) = await OpenAsync(harness);
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var hr = harness.Context.Services.GetRequiredService<HrService>();
        var draft = view.ProfileEditor;
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        await cut.Find("[data-testid='crmhr-workforce-notes']").InputAsync(new ChangeEventArgs { Value = "  Unblurred workforce notes  " });
        var peer = (await hr.GetWorkforceProfileWorkspaceAsync(party))!.Profile;
        peer.JobTitle = "Peer title";
        Assert.True((await hr.SaveWorkforceProfileAsync(peer)).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-workforce-profile-outcome']");
        Assert.Same(draft, view.ProfileEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        await cut.Find("[data-testid='crmhr-workforce-review-open']").ClickAsync(new());
        cut.WaitForElement("[data-testid='crmhr-workforce-review-dialog']");
        await cut.Find("[data-testid='crmhr-workforce-review-apply']").ClickAsync(new());

        Assert.Equal("Peer title", draft.JobTitle);
        Assert.Equal("  Unblurred workforce notes  ", draft.Notes);
        Assert.Empty((await hr.GetWorkforceProfileWorkspaceAsync(party))!.Profile.Notes);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        await cut.Find("[data-testid='crmhr-workforce-profile-form']").SubmitAsync();
        Assert.Equal("Unblurred workforce notes", (await hr.GetWorkforceProfileWorkspaceAsync(party))!.Profile.Notes);
        Assert.Same(draft, view.ProfileEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
    }

    [Fact]
    public async Task Known_native_profile_acceptance_preserves_later_notes_and_invalid_date_in_the_same_form() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        var (cut, party) = await OpenAsync(harness);
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var draft = view.ProfileEditor;
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        await cut.Find("[data-testid='crmhr-workforce-job-title']").InputAsync(new ChangeEventArgs { Value = "  Submitted workforce title  " });
        control.HoldWriteIn(nameof(WorkforceProfileEditingService.SaveWorkforceProfileAsync));
        var pending = cut.Find("[data-testid='crmhr-workforce-profile-form']").SubmitAsync();
        await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-workforce-notes']").InputAsync(new ChangeEventArgs { Value = "  Later workforce notes  " }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-workforce-end-date']").TriggerEventAsync("oncadnativeinput", new NativeInputEventArgs { Value = "not-a-date", BadInput = true }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-workforce-end-date']").ChangeAsync(new ChangeEventArgs { Value = "not-a-date" }));
        control.Release();
        await pending.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Same(draft, view.ProfileEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Equal("  Later workforce notes  ", draft.Notes);
        Assert.Contains(context.GetValidationMessages(), message => message.Contains("date", StringComparison.OrdinalIgnoreCase));
        Assert.True(cut.Find("[data-testid='crmhr-workforce-save-button']").HasAttribute("disabled"));
        var saved = (await harness.Context.Services.GetRequiredService<HrService>().GetWorkforceProfileWorkspaceAsync(party))!.Profile;
        Assert.Equal("Submitted workforce title", saved.JobTitle);
        Assert.Empty(saved.Notes);
        Assert.NotEqual(default, saved.EndDate);
    }

    [Fact]
    public async Task Dirty_close_requires_a_current_decision_and_a_disposed_form_cannot_submit_the_next_opening() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, party) = await OpenAsync(harness);
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var oldForm = cut.FindComponent<WorkforceProfileForm>().Instance;
        await cut.Find("[data-testid='crmhr-workforce-notes']").InputAsync(new ChangeEventArgs { Value = "First draft" });
        var closing = cut.Find("[data-testid='crmhr-workforce-record-close']").ClickAsync(new());
        cut.WaitForElement("[data-testid='crmhr-workforce-dirty-close']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-workforce-notes']").InputAsync(new ChangeEventArgs { Value = "Later draft" }));
        await cut.Find("[data-testid='crmhr-workforce-dirty-discard']").ClickAsync(new());
        await closing;
        Assert.True(view.IsRecordDialogOpen);
        Assert.Equal("Later draft", view.ProfileEditor.Notes);
        closing = cut.Find("[data-testid='crmhr-workforce-record-close']").ClickAsync(new());
        cut.WaitForElement("[data-testid='crmhr-workforce-dirty-close']");
        await cut.Find("[data-testid='crmhr-workforce-dirty-discard']").ClickAsync(new());
        await closing;
        Assert.False(view.IsRecordDialogOpen);
        cut.WaitForAssertion(() => Assert.Empty(cut.FindComponents<WorkforceProfileForm>()));
        cut.WaitForElement("[data-testid='crmhr-workforce-item']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-workforce-item']").ClickAsync(new()));
        Assert.True(view.IsRecordDialogOpen);
        Assert.NotNull(view.SelectedWorkspace);
        Assert.Equal(party, view.SelectedWorkspace.PartyId);
        Assert.NotNull(view.SelectedWorkspace.Profile.Id);
        Assert.False(view.SelectedIsExternalContactWithoutProfile);
        cut.WaitForElement("[data-testid='crmhr-workforce-tab-profile']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-workforce-tab-profile']").ClickAsync(new()));
        cut.WaitForElement("[data-testid='crmhr-workforce-job-title']");
        Assert.NotSame(oldForm, cut.FindComponent<WorkforceProfileForm>().Instance);
        var next = view.ProfileEditor;

        await cut.InvokeAsync(oldForm.SubmitAsync);

        Assert.Same(next, view.ProfileEditor);
        Assert.Empty((await harness.Context.Services.GetRequiredService<HrService>().GetWorkforceProfileWorkspaceAsync(party))!.Profile.Notes);
    }

    [Fact]
    public async Task Nested_reference_picker_cancel_retains_raw_profile_and_one_context() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, _) = await OpenAsync(harness);
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var draft = view.ProfileEditor;
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        await cut.Find("[data-testid='crmhr-workforce-notes']").InputAsync(new ChangeEventArgs { Value = "  Picker retains raw notes  " });
        await cut.Find("[data-testid='crmhr-workforce-manager-select']").ClickAsync(new());
        cut.WaitForElement("[data-testid='crmhr-workforce-manager-dialog']");
        Assert.True(cut.Find("[data-testid='crmhr-workforce-save-button']").HasAttribute("disabled"));
        await cut.Find("[data-testid='crmhr-workforce-manager-dialog-cancel']").ClickAsync(new());

        Assert.Same(draft, view.ProfileEditor);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Equal("  Picker retains raw notes  ", draft.Notes);
        Assert.False(cut.Find("[data-testid='crmhr-workforce-save-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Switching_workforce_tabs_keeps_the_form_context_and_unparsed_date_control() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, _) = await OpenAsync(harness);
        var view = (ICrmHrWorkforceWorkspaceView)cut.Instance;
        var draft = view.ProfileEditor;
        var context = ComponentTestHarness.FindEditContext(cut, draft);
        var form = cut.FindComponent<WorkforceProfileForm>().Instance;
        var date = cut.FindComponents<NativeValueInput<DateOnly?>>().Single(component => component.Instance.Value == draft.EndDate).Instance;
        await cut.Find("[data-testid='crmhr-workforce-end-date']").TriggerEventAsync("oncadnativeinput", new NativeInputEventArgs { Value = "not-a-date", BadInput = true });
        await cut.Find("[data-testid='crmhr-workforce-end-date']").ChangeAsync(new ChangeEventArgs { Value = "not-a-date" });

        await cut.Find("[data-testid='crmhr-workforce-tab-skills']").ClickAsync(new());
        await cut.Find("[data-testid='crmhr-workforce-tab-profile']").ClickAsync(new());

        Assert.Same(form, cut.FindComponent<WorkforceProfileForm>().Instance);
        Assert.Same(context, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Contains(cut.FindComponents<NativeValueInput<DateOnly?>>(), component => ReferenceEquals(component.Instance, date));
        Assert.Contains(context.GetValidationMessages(), message => message.Contains("date", StringComparison.OrdinalIgnoreCase));
        Assert.True(cut.Find("[data-testid='crmhr-workforce-save-button']").HasAttribute("disabled"));
    }

    private static async Task<(IRenderedComponent<CrmHrWorkforcePage> Page, Guid Party)> OpenAsync(ComponentTestHarness harness) {
        var services = harness.Context.Services;
        var party = await services.GetRequiredService<PartyDirectoryService>().SavePartyAsync(new() {
            PartyType = PartyType.Person, LifecycleStatus = PartyLifecycleStatus.Active, DisplayName = "Native workforce editor"
        });
        Assert.True(party.IsSuccess);
        var hr = services.GetRequiredService<HrService>();
        var profile = (await hr.GetWorkforceProfileWorkspaceAsync(party.Value))!.Profile;
        profile.JobTitle = "Original title";
        profile.StartDate = new(2026, 11, 2);
        profile.EndDate = new(2026, 11, 4);
        Assert.True((await hr.SaveWorkforceProfileAsync(profile)).IsSuccess);
        services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/workforce?partyId={party.Value:D}&section=profile");
        var cut = harness.Context.Render<CrmHrWorkforcePage>();
        cut.WaitForElement("[data-testid='crmhr-workforce-job-title']");
        return (cut, party.Value);
    }
}
