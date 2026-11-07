using Bunit;
using CanDoItAll.Modules.CrmHr;
using CanDoItAll.Modules.CrmHr.Pages;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.CrmHr;

// Mutation lifetimes of the Assignments host through its real component tree and view contract: one admitted write per
// project selection, an independent submission with the captured project admission, and completions of a retired
// selection that never touch the project selected since. The owner write is a deterministic seam; everything else is
// the production host.
public sealed class CrmHrAssignmentsMutationTests
{
    [Fact]
    public async Task Unknown_save_is_read_only_until_records_are_reviewed_and_local_discard_is_explicitly_confirmed() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var project = Project("Unknown assignment context");
        var cut = harness.Context.Render<SavingAssignmentsPage>(parameters => parameters.Add(page => page.TestProjects, [project]));
        var view = (ICrmHrAssignmentsWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(project.Id, view.SelectedProjectId));
        Assert.True(await cut.InvokeAsync(view.PrepareRelationshipCreateAsync));
        view.DraftAssignment.PartyId = Guid.NewGuid();
        view.DraftAssignment.Notes = "Retain the uncertain submission";
        cut.Instance.FailNextSave(new IOException("The command outcome could not be observed."));

        Assert.False(await cut.InvokeAsync(view.SaveDraftAsync));
        Assert.True(view.HasUnknownOperation);
        Assert.False(view.CanDiscardReviewedUnknown);
        Assert.False(await cut.InvokeAsync(view.SaveDraftAsync));
        Assert.Single(cut.Instance.Submissions);
        await cut.InvokeAsync(view.RefreshRecordsAsync);
        Assert.True(view.CanDiscardReviewedUnknown);
        var keep = cut.InvokeAsync(view.DiscardReviewedUnknownAsync);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='crmhr-assignments-unknown-dirty-continue']")));
        await cut.Find("[data-testid='crmhr-assignments-unknown-dirty-continue']").ClickAsync(new());
        await keep;
        Assert.True(view.HasUnknownOperation);
        Assert.Equal("Retain the uncertain submission", view.DraftAssignment.Notes);

        var discard = cut.InvokeAsync(view.DiscardReviewedUnknownAsync);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='crmhr-assignments-unknown-dirty-discard']")));
        await cut.Find("[data-testid='crmhr-assignments-unknown-dirty-discard']").ClickAsync(new());
        await discard;

        Assert.False(view.HasUnknownOperation);
        Assert.Equal(Guid.Empty, view.DraftAssignment.PartyId);
        Assert.Single(cut.Instance.Submissions);
        Assert.Contains("remains unconfirmed", view.EditingMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_second_dispatch_is_dropped_while_the_first_assignment_save_is_in_flight()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var project = Project("Single write project");
        var cut = harness.Context.Render<SavingAssignmentsPage>(parameters => parameters.Add(page => page.TestProjects, [project]));
        var view = (ICrmHrAssignmentsWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(project.Id, view.SelectedProjectId));
        Assert.True(await cut.InvokeAsync(view.PrepareRelationshipCreateAsync));
        var partyId = Guid.NewGuid();
        view.DraftAssignment.PartyId = partyId;
        view.DraftAssignment.Notes = "submitted notes";
        cut.Instance.HoldSaves();

        var first = cut.InvokeAsync(view.SaveDraftAsync);
        await cut.Instance.WaitForSaveAsync();
        // Enter on the form while the button's save is still writing.
        Assert.False(await cut.InvokeAsync(view.SaveDraftAsync));
        // Typing after the dispatch never reaches the submission the owner received.
        view.DraftAssignment.Notes = "typed after dispatch";
        cut.Instance.CompleteSave(Result<Guid>.Success(Guid.NewGuid()));
        Assert.True(await first);

        var submission = Assert.Single(cut.Instance.Submissions);
        Assert.Equal(partyId, submission.PartyId);
        Assert.Equal("submitted notes", submission.Notes);
        Assert.Equal(project.Id, submission.ProjectId);
        var profileId = harness.Context.Services.GetRequiredService<ProjectWriteAdmissionService>().DatabaseProfileId;
        Assert.Equal(new ProjectWriteAdmission(profileId, project.Id, project.LifetimeId!.Value), submission.ExpectedProjectAdmission);

        var nextCreate = cut.InvokeAsync(view.PrepareRelationshipCreateAsync);
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='crmhr-assignment-dirty-discard']")));
        await cut.Find("[data-testid='crmhr-assignment-dirty-discard']").ClickAsync(new());
        Assert.True(await nextCreate);
        view.DraftAssignment.PartyId = Guid.NewGuid();
        cut.Instance.CompleteNextSaveImmediately(Result<Guid>.Success(Guid.NewGuid()));
        Assert.True(await cut.InvokeAsync(view.SaveDraftAsync));
        Assert.Equal(2, cut.Instance.Submissions.Count);
    }

    [Fact]
    public async Task Project_retarget_waits_for_the_save_and_requires_explicit_discard_of_later_input() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var first = Project("Pending project");
        var second = Project("Next project");
        var cut = harness.Context.Render<SavingAssignmentsPage>(parameters => parameters.Add(page => page.TestProjects, [first, second]));
        var view = (ICrmHrAssignmentsWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(first.Id, view.SelectedProjectId));
        Assert.True(await cut.InvokeAsync(view.PrepareRelationshipCreateAsync));
        view.DraftAssignment.PartyId = Guid.NewGuid();
        cut.Instance.HoldSaves();
        var saving = cut.InvokeAsync(view.SaveDraftAsync);
        await cut.Instance.WaitForSaveAsync();

        await cut.InvokeAsync(() => view.HandleProjectChanged(second.Id));

        Assert.Equal(first.Id, view.SelectedProjectId);
        var live = view.DraftAssignment;
        live.Notes = "Input entered during the save";
        cut.Instance.CompleteSave(Result<Guid>.Success(Guid.NewGuid()));
        Assert.True(await saving);
        Assert.Same(live, view.DraftAssignment);
        Assert.Equal("Input entered during the save", live.Notes);
        Assert.NotNull(live.AssignmentId);
        Assert.NotNull(live.Original);
        var navigation = cut.InvokeAsync(() => view.HandleProjectChanged(second.Id));
        cut.WaitForAssertion(() => Assert.Single(cut.FindAll("[data-testid='crmhr-assignments-dirty-discard']")));
        await cut.Find("[data-testid='crmhr-assignments-dirty-discard']").ClickAsync(new());
        await navigation;
        cut.WaitForAssertion(() => Assert.Equal(second.Id, view.SelectedProjectId));
        Assert.Equal(second.Id, view.DraftAssignment.ProjectId);
        Assert.Null(view.DraftAssignment.AssignmentId);
        Assert.Equal(first.Id, Assert.Single(cut.Instance.Submissions).ProjectId);
    }

    [Fact]
    public async Task Refreshing_skill_choices_preserves_the_staffing_form_and_pending_input() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var project = Project("Skill catalog project");
        var cut = harness.Context.Render<SavingAssignmentsPage>(parameters => parameters.Add(page => page.TestProjects, [project]));
        var view = await OpenStaffingAsync(cut);
        var draft = view.StaffingRequestDraft;
        var form = ComponentTestHarness.FindEditContext(cut, draft);
        var input = cut.FindComponent<CanDoItAll.Components.BaseLib.NativeValueInput<decimal>>().Instance;
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-staffing-request-title']").InputAsync(
            new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = "Retained staffing draft" }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-staffing-request-allocation']").TriggerEventAsync(
            "oncadnativeinput", new CanDoItAll.Components.BaseLib.NativeInputEventArgs { Value = "", BadInput = true }));
        cut.Instance.TestSkills.Add(new(Guid.NewGuid(), "New peer skill", "Engineering", "", true));

        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-staffing-request-refresh-skills']").ClickAsync(new()));

        cut.WaitForAssertion(() => Assert.Contains("New peer skill", cut.Find("[data-testid='crmhr-staffing-request-dialog']").TextContent));
        Assert.Same(draft, view.StaffingRequestDraft);
        Assert.Equal("Retained staffing draft", draft.Title);
        Assert.Same(form, ComponentTestHarness.FindEditContext(cut, draft));
        Assert.Same(input, cut.FindComponent<CanDoItAll.Components.BaseLib.NativeValueInput<decimal>>().Instance);
        Assert.True(cut.Find("[data-testid='crmhr-staffing-request-save-button']").HasAttribute("disabled"));
    }

    [Fact]
    public async Task Inactive_selected_skills_stay_until_explicitly_removed() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var project = Project("Unavailable skill project");
        var cut = harness.Context.Render<SavingAssignmentsPage>(parameters => parameters.Add(page => page.TestProjects, [project]));
        var skill = new SkillCatalogItemModel(Guid.NewGuid(), "Selected skill", "Engineering", "", true);
        cut.Instance.TestSkills.Add(skill);
        var view = await OpenStaffingAsync(cut);
        var draft = view.StaffingRequestDraft;
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-staffing-request-skill']").ChangeAsync(
            new Microsoft.AspNetCore.Components.ChangeEventArgs { Value = true }));
        cut.Instance.TestSkills[0] = skill with { IsActive = false };

        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-staffing-request-refresh-skills']").ClickAsync(new()));

        cut.WaitForElement("[data-testid='crmhr-staffing-request-unavailable-skill']");
        Assert.Contains(skill.Id, draft.SkillIds);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-staffing-request-skill']"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-staffing-request-remove-unavailable-skill']").ClickAsync(new()));
        Assert.Empty(draft.SkillIds);
        Assert.Same(draft, view.StaffingRequestDraft);
        Assert.Empty(cut.FindAll("[data-testid='crmhr-staffing-request-unavailable-skill']"));
    }

    private static async Task<ICrmHrAssignmentsWorkspaceView> OpenStaffingAsync(IRenderedComponent<SavingAssignmentsPage> cut) {
        var view = (ICrmHrAssignmentsWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.NotNull(view.SelectedProjectId));
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-assignments-tab-staffing']").ClickAsync(new()));
        cut.WaitForElement("[data-testid='crmhr-staffing-request-create-button']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='crmhr-staffing-request-create-button']").ClickAsync(new()));
        cut.WaitForElement("[data-testid='crmhr-staffing-request-dialog']");
        return view;
    }

    private static ProjectRecordQueryItem Project(string name)
        => new(Guid.NewGuid(), name, ProjectStatus.Active, "Delivery", string.Empty, DateTimeOffset.UtcNow) { LifetimeId = Guid.NewGuid() };

    private sealed class SavingAssignmentsPage : CrmHrAssignmentsPage
    {
        private TaskCompletionSource<Result<Guid>>? heldSave;
        private TaskCompletionSource saveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Result<Guid>? immediateResult;
        private Exception? nextFailure;

        [Microsoft.AspNetCore.Components.Parameter]
        public IReadOnlyList<ProjectRecordQueryItem> TestProjects { get; set; } = [];

        public List<ProjectPartyAssignmentUpsertRequest> Submissions { get; } = [];
        public List<SkillCatalogItemModel> TestSkills { get; } = [];

        public void HoldSaves()
        {
            heldSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
            saveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task WaitForSaveAsync() => saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void CompleteSave(Result<Guid> result) => heldSave!.TrySetResult(result);

        public void CompleteNextSaveImmediately(Result<Guid> result) => immediateResult = result;
        public void FailNextSave(Exception failure) => nextFailure = failure;

        protected override async Task<Result<Guid>> SaveAssignmentOwnerAsync(ProjectPartyAssignmentUpsertRequest submission,
            CancellationToken cancellationToken, Action<ProjectAssignmentSaveReceipt> onCommitted) {
            Submissions.Add(submission);
            if (nextFailure is { } failure) {
                nextFailure = null;
                throw failure;
            }
            Result<Guid> result;
            if (immediateResult is { } immediate) {
                immediateResult = null;
                result = immediate;
            } else {
                saveStarted.TrySetResult();
                result = await heldSave!.Task;
            }
            if (result.IsSuccess) {
                onCommitted(new(result.Value, new(result.Value, submission.ProjectId, submission.ExpectedProjectAdmission!.LifetimeId,
                    submission.PartyId, submission.PartyAffiliationId, submission.Role, submission.NodeKey.Trim(), string.Empty, null,
                    submission.IsPrimary, submission.AllocationPercent, null, null,
                    string.IsNullOrWhiteSpace(submission.Source) ? "crm-hr-ui" : submission.Source.Trim(), submission.Notes.Trim())));
            }
            return result;
        }

        protected override Task<AssignmentProjectCatalog> LoadProjectCatalogAsync(Guid? preferredProjectId, bool allowFallback, CancellationToken cancellationToken)
        {
            var selected = preferredProjectId.HasValue ? TestProjects.FirstOrDefault(project => project.Id == preferredProjectId.Value) : null;
            selected ??= allowFallback ? TestProjects.FirstOrDefault() : null;
            return Task.FromResult(new AssignmentProjectCatalog(selected, TestProjects.Count));
        }

        protected override Task<ProjectRecordQueryItem?> GetProjectAsync(Guid projectId, CancellationToken cancellationToken)
            => Task.FromResult(TestProjects.FirstOrDefault(project => project.Id == projectId));

        protected override Task<AssignmentSelectionSnapshot> LoadSelectionSnapshotAsync(AssignmentSelectionLoadRequest request, CancellationToken cancellationToken)
            => Task.FromResult(new AssignmentSelectionSnapshot(request.RequestedData, [], TestSkills.ToArray(), [], []));

        protected override Task<StaffingDashboardModel> GetStaffingDashboardAsync(CancellationToken cancellationToken)
            => Task.FromResult(new StaffingDashboardModel(0, 0m, 0, 0));
    }
}
