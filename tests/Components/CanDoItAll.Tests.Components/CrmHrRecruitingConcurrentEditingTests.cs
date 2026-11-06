using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.CrmHr.UI.Recruiting;
using CanDoItAll.CrmHr.UI.Pickers;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

public sealed class CrmHrRecruitingConcurrentEditingTests {
    [Fact]
    public async Task A_peer_application_commit_refreshes_the_catalog_without_replacing_the_open_application() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, owner, id) = await OpenAsync(harness);
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        var draft = view.ApplicationEditor;
        var context = Context(cut, draft);
        var peer = (await owner.GetRecruitmentWorkspaceAsync(id)).Application;
        peer.DesiredRole = "Catalog peer recruiting role";

        Assert.True((await owner.SaveRecruitmentApplicationAsync(peer)).IsSuccess);

        cut.WaitForAssertion(() => Assert.Contains("Catalog peer recruiting role",
            cut.Find($"[data-testid='crmhr-recruiting-application-{id:N}']").TextContent, StringComparison.Ordinal));
        Assert.Same(draft, view.ApplicationEditor);
        Assert.Same(context, Context(cut, draft));
        Assert.Equal("Original role", draft.DesiredRole);
    }

    [Fact]
    public async Task Resetting_a_task_preserves_the_independent_support_form_and_picker_lifetime() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, _, _) = await OpenAsync(harness);
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        var support = view.SupportEditor;
        var supportContext = Context(cut, support);
        var buddy = cut.FindComponents<PartyPicker>().Single(component => component.Instance.TestIdPrefix == "crmhr-recruiting-support-buddy").Instance;
        var task = view.TaskEditor;
        await cut.Find("[data-testid='crmhr-recruiting-tab-development']").ClickAsync(new());

        await cut.Find("[data-testid='crmhr-recruiting-task-reset-button']").ClickAsync(new());

        Assert.NotSame(task, view.TaskEditor);
        Assert.Same(support, view.SupportEditor);
        Assert.Same(supportContext, Context(cut, support));
        Assert.Same(buddy, cut.FindComponents<PartyPicker>().Single(component => component.Instance.TestIdPrefix == "crmhr-recruiting-support-buddy").Instance);
    }
    [Fact]
    public async Task A_support_comparison_keeps_local_buddy_input_and_saves_only_after_apply() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, owner, id) = await OpenAsync(harness);
        var parties = harness.Context.Services.GetRequiredService<PartyDirectoryService>();
        var manager = (await parties.SavePartyAsync(new() { DisplayName = "Peer manager", PartyType = PartyType.Person })).Value;
        var buddy = (await parties.SavePartyAsync(new() { DisplayName = "Local buddy", PartyType = PartyType.Person })).Value;
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        var draft = view.SupportEditor;
        var context = Context(cut, draft);
        await cut.Find("[data-testid='crmhr-recruiting-tab-development']").ClickAsync(new());
        var picker = cut.FindComponents<PartyPicker>().Single(component => component.Instance.TestIdPrefix == "crmhr-recruiting-support-buddy");
        await cut.InvokeAsync(() => picker.Instance.SelectedPartyIdChanged.InvokeAsync(buddy));
        var peer = RecruitmentRecordDrafts.FromSupport((await owner.GetRecruitmentWorkspaceAsync(id)).SupportAssignments);
        peer.ManagerPartyId = manager;
        Assert.True((await owner.SaveSupportAssignmentsAsync(peer)).IsSuccess);
        await cut.WaitForElement("[data-testid='crmhr-recruiting-support-review-open']").ClickAsync(new());
        await cut.WaitForElement("[data-testid='crmhr-recruiting-support-review-apply']").ClickAsync(new());

        Assert.Same(draft, view.SupportEditor);
        Assert.Same(context, Context(cut, draft));
        Assert.Equal(manager, draft.ManagerPartyId);
        Assert.Equal(buddy, draft.BuddyPartyId);
        Assert.Null((await owner.GetRecruitmentWorkspaceAsync(id)).SupportAssignments.BuddyPartyId);
        await cut.Find("[data-testid='crmhr-recruiting-support-form']").SubmitAsync();
        Assert.Equal(buddy, (await owner.GetRecruitmentWorkspaceAsync(id)).SupportAssignments.BuddyPartyId);
    }

    [Fact]
    public async Task A_native_task_connection_failure_keeps_the_unknown_submission_after_refresh_and_blocks_repeating_it() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        var (cut, owner, id) = await OpenAsync(harness);
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        var draft = view.TaskEditor;
        var context = Context(cut, draft);
        await cut.Find("[data-testid='crmhr-recruiting-tab-development']").ClickAsync(new());
        await cut.Find("[data-testid='crmhr-recruiting-task-title']").InputAsync(new() { Value = "Submitted task" });
        var picker = cut.FindComponents<PartyPicker>().Single(component => component.Instance.TestIdPrefix == "crmhr-recruiting-task-owner");
        await cut.InvokeAsync(() => picker.Instance.SelectedPartyIdChanged.InvokeAsync(view.ApplicationEditor.PartyId));
        control.FailWriteIn(nameof(RecruitmentLifecycleEditingService.SaveLifecycleTaskAsync));
        await cut.Find("[data-testid='crmhr-recruiting-task-form']").SubmitAsync();
        await cut.Find("[data-testid='crmhr-recruiting-task-title']").InputAsync(new() { Value = "Later task" });
        await cut.Find("[data-testid='crmhr-recruiting-refresh']").ClickAsync(new());

        Assert.Same(draft, view.TaskEditor);
        Assert.Same(context, Context(cut, draft));
        Assert.Equal("Later task", draft.Title);
        Assert.Contains("unknown", cut.Find("[data-testid='crmhr-recruiting-record-outcome']").TextContent, StringComparison.OrdinalIgnoreCase);
        Assert.True(cut.Find("[data-testid='crmhr-recruiting-task-save-button']").HasAttribute("disabled"));
        await cut.Find("[data-testid='crmhr-recruiting-task-form']").SubmitAsync();
        Assert.Empty((await owner.GetRecruitmentWorkspaceAsync(id)).LifecycleTasks);
        Assert.Equal(1, control.Interceptions);
    }

    [Fact]
    public async Task Inline_creation_keeps_later_candidate_details_visible_until_an_explicit_saved_identity_choice() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/crm-hr/recruiting?dialog=new-application");
        var cut = harness.Context.Render<CrmHrRecruitingPage>();
        await cut.WaitForElement("[data-testid='crmhr-recruiting-candidate-name']").InputAsync(new() { Value = "Submitted candidate" });
        await cut.Find("[data-testid='crmhr-recruiting-role']").InputAsync(new() { Value = "Submitted role" });
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        var draft = view.ApplicationEditor;
        var context = Context(cut, draft);
        control.HoldWriteIn(nameof(RecruitmentApplicationEditingService.SaveRecruitmentApplicationAsync));
        var saving = cut.Find("[data-testid='crmhr-recruiting-application-form']").SubmitAsync();
        try {
            await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-recruiting-candidate-name']").InputAsync(new() { Value = "Later candidate" }));
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-recruiting-role']").InputAsync(new() { Value = "Later role" }));
        } finally {
            control.Release();
        }
        await saving.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Same(draft, view.ApplicationEditor);
        Assert.Same(context, Context(cut, draft));
        Assert.NotNull(draft.Id);
        Assert.Equal("Later candidate", draft.CandidateName);
        Assert.Equal("Later role", draft.DesiredRole);
        Assert.False(cut.Find("[data-testid='crmhr-recruiting-candidate-name']").HasAttribute("disabled"));
        cut.Find("[data-testid='crmhr-recruiting-later-candidate-input']");
        Assert.True(cut.Find("[data-testid='crmhr-recruiting-save-button']").HasAttribute("disabled"));
        var saved = await harness.Context.Services.GetRequiredService<RecruitingService>().GetRecruitmentWorkspaceAsync(draft.Id);
        Assert.Equal("Submitted candidate", saved.CandidateDisplayName);
        Assert.Equal("Submitted role", saved.Application.DesiredRole);
    }
    [Fact]
    public async Task A_clean_application_waits_for_explicit_pull_and_keeps_its_form_context() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, owner, id) = await OpenAsync(harness);
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        var draft = view.ApplicationEditor;
        var context = Context(cut, draft);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-application-outcome']"));
        var peer = (await owner.GetRecruitmentWorkspaceAsync(id)).Application;
        peer.DesiredRole = "Peer role";

        Assert.True((await owner.SaveRecruitmentApplicationAsync(peer)).IsSuccess);
        cut.WaitForElement("[data-testid='crmhr-recruiting-application-outcome']");
        Assert.Equal("Original role", draft.DesiredRole);
        Assert.True(cut.Find("[data-testid='crmhr-recruiting-save-button']").HasAttribute("disabled"));
        await cut.Find("[data-testid='crmhr-recruiting-refresh']").ClickAsync(new());

        Assert.Same(draft, view.ApplicationEditor);
        Assert.Same(context, Context(cut, draft));
        Assert.Equal("Peer role", draft.DesiredRole);
        Assert.False(cut.Find("[data-testid='crmhr-recruiting-save-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task An_application_comparison_keeps_unblurred_input_and_requires_a_separate_save() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, owner, id) = await OpenAsync(harness);
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        var draft = view.ApplicationEditor;
        var context = Context(cut, draft);
        await cut.Find("[data-testid='crmhr-recruiting-role']").InputAsync(new() { Value = "  Local unblurred role  " });
        var peer = (await owner.GetRecruitmentWorkspaceAsync(id)).Application;
        peer.Source = "Peer source";
        Assert.True((await owner.SaveRecruitmentApplicationAsync(peer)).IsSuccess);
        await cut.WaitForElement("[data-testid='crmhr-recruiting-review-open']").ClickAsync(new());
        await cut.WaitForElement("[data-testid='crmhr-recruiting-review-apply']").ClickAsync(new());

        Assert.Same(draft, view.ApplicationEditor);
        Assert.Same(context, Context(cut, draft));
        Assert.Equal("  Local unblurred role  ", draft.DesiredRole);
        Assert.Equal("Peer source", draft.Source);
        Assert.Equal("Original role", (await owner.GetRecruitmentWorkspaceAsync(id)).Application.DesiredRole);
        await cut.Find("[data-testid='crmhr-recruiting-application-form']").SubmitAsync();
        Assert.Equal("Local unblurred role", (await owner.GetRecruitmentWorkspaceAsync(id)).Application.DesiredRole);
    }

    [Fact]
    public async Task A_held_interview_save_keeps_later_text_and_unparsed_time_after_refresh() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        var (cut, owner, id) = await OpenAsync(harness);
        await cut.Find("[data-testid='crmhr-recruiting-tab-interviews']").ClickAsync(new());
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        var draft = view.InterviewEditor;
        var context = Context(cut, draft);
        await cut.Find("[data-testid='crmhr-recruiting-interview-feedback']").InputAsync(new() { Value = "Submitted feedback" });
        control.HoldWriteIn(nameof(RecruitmentInterviewEditingService.SaveRecruitmentInterviewAsync));
        var saving = cut.Find("[data-testid='crmhr-recruiting-interview-form']").SubmitAsync();
        try {
            await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-recruiting-interview-feedback']").InputAsync(new() { Value = "  Later feedback  " }));
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-recruiting-interview-scheduled']").TriggerEventAsync("oncadnativeinput", new NativeInputEventArgs { Value = "incomplete", BadInput = true }));
        } finally {
            control.Release();
        }
        await saving.WaitAsync(TimeSpan.FromSeconds(30));
        await cut.Find("[data-testid='crmhr-recruiting-refresh']").ClickAsync(new());

        Assert.Same(draft, view.InterviewEditor);
        Assert.Same(context, Context(cut, draft));
        Assert.Equal("  Later feedback  ", draft.Feedback);
        Assert.Null(draft.Id);
        Assert.Contains(context.GetValidationMessages(), message => message.Contains("date", StringComparison.OrdinalIgnoreCase));
        Assert.True(cut.Find("[data-testid='crmhr-recruiting-interview-save-button']").HasAttribute("disabled"));
        Assert.Equal("Submitted feedback", Assert.Single((await owner.GetRecruitmentWorkspaceAsync(id)).Interviews).Feedback);
        Assert.Equal(1, control.Interceptions);
    }

    [Fact]
    public async Task A_conversion_receipt_keeps_later_conversion_and_application_input_over_its_saved_preflight() {
        CrmHrOwnerReadBackControl control = null!;
        await using var harness = await ComponentTestHarness.CreateAsync(services => control = CrmHrOwnerReadBackControl.Install(services));
        var (cut, owner, id) = await OpenAsync(harness);
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        await cut.Find("[data-testid='crmhr-recruiting-tab-conversion']").ClickAsync(new());
        var conversion = view.ConversionEditor;
        var application = view.ApplicationEditor;
        var context = Context(cut, conversion);
        await cut.Find("[data-testid='crmhr-recruiting-convert-job-title']").InputAsync(new() { Value = "Submitted title" });
        control.HoldWriteIn(nameof(RecruitmentConversionService.ConvertCandidateAsync));
        var saving = cut.Find("[data-testid='crmhr-recruiting-conversion-form']").SubmitAsync();
        try {
            await control.HoldReached.WaitAsync(TimeSpan.FromSeconds(30));
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-recruiting-convert-job-title']").InputAsync(new() { Value = "  Later title  " }));
            await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-recruiting-role']").InputAsync(new() { Value = "  Later application role  " }));
        } finally {
            control.Release();
        }
        await saving.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.Same(conversion, view.ConversionEditor);
        Assert.Same(context, Context(cut, conversion));
        Assert.Same(application, view.ApplicationEditor);
        Assert.Equal("  Later title  ", conversion.JobTitle);
        Assert.Equal("  Later application role  ", application.DesiredRole);
        Assert.Equal(RecruitmentStage.Hired, application.Original!.Stage);
        var saved = await owner.GetRecruitmentWorkspaceAsync(id);
        Assert.Equal("Submitted title", saved.Conversion.JobTitle);
        Assert.Equal("Original role", saved.Application.DesiredRole);
        Assert.Equal(saved.Conversion.Original, conversion.Original);
        Assert.Equal(1, control.Interceptions);
    }

    [Fact]
    public async Task Dirty_close_keeps_the_form_until_the_operator_explicitly_discards() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, _, _) = await OpenAsync(harness);
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        var draft = view.ApplicationEditor;
        await cut.Find("[data-testid='crmhr-recruiting-role']").InputAsync(new() { Value = "Local role" });
        var closing = cut.InvokeAsync(view.CloseRecruitmentDialogAsync);
        await cut.WaitForElement("[data-testid='crmhr-recruiting-dirty-continue']").ClickAsync(new());
        await closing;
        Assert.Same(draft, view.ApplicationEditor);
        Assert.Equal("Local role", draft.DesiredRole);
        cut.Find("[data-testid='crmhr-recruiting-record-dialog']");
        closing = cut.InvokeAsync(view.CloseRecruitmentDialogAsync);
        await cut.WaitForElement("[data-testid='crmhr-recruiting-dirty-discard']").ClickAsync(new());
        await closing;
        Assert.Empty(cut.FindAll("[data-testid='crmhr-recruiting-record-dialog']"));
    }

    [Fact]
    public async Task Switching_record_sections_keeps_incomplete_local_time_in_one_form() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var (cut, _, _) = await OpenAsync(harness);
        var view = (ICrmHrRecruitingWorkspaceView)cut.Instance;
        var draft = view.InterviewEditor;
        var context = Context(cut, draft);
        var form = cut.FindComponent<InterviewSchedulePanel>().Instance;
        await cut.Find("[data-testid='crmhr-recruiting-interview-scheduled']").TriggerEventAsync("oncadnativeinput", new NativeInputEventArgs { Value = "incomplete", BadInput = true });
        await cut.Find("[data-testid='crmhr-recruiting-tab-development']").ClickAsync(new());
        await cut.Find("[data-testid='crmhr-recruiting-tab-interviews']").ClickAsync(new());
        Assert.Same(form, cut.FindComponent<InterviewSchedulePanel>().Instance);
        Assert.Same(context, Context(cut, draft));
        Assert.Contains(context.GetValidationMessages(), message => message.Contains("date", StringComparison.OrdinalIgnoreCase));
        Assert.True(cut.Find("[data-testid='crmhr-recruiting-interview-save-button']").HasAttribute("disabled"));
    }

    private static EditContext Context(IRenderedComponent<CrmHrRecruitingPage> cut, object model) =>
        cut.FindComponents<EditForm>().Single(form => ReferenceEquals(form.Instance.EditContext.Model, model)).Instance.EditContext;

    private static async Task<(IRenderedComponent<CrmHrRecruitingPage> Page, RecruitingService Owner, Guid Id)> OpenAsync(ComponentTestHarness harness) {
        var owner = harness.Context.Services.GetRequiredService<RecruitingService>();
        var result = await owner.SaveRecruitmentApplicationAsync(new() {
            CandidateName = "Native recruiting candidate", DesiredRole = "Original role", Stage = RecruitmentStage.Offer,
            Decision = RecruitmentDecision.Approved, AvailableFrom = new(2026, 11, 2)
        });
        Assert.True(result.IsSuccess);
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/crm-hr/recruiting?applicationId={result.Value:D}");
        var cut = harness.Context.Render<CrmHrRecruitingPage>();
        cut.WaitForElement("[data-testid='crmhr-recruiting-role']");
        return (cut, owner, result.Value);
    }
}
