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

        // The gate is released with the owner's answer: the next save is admitted again.
        Assert.True(await cut.InvokeAsync(view.PrepareRelationshipCreateAsync));
        view.DraftAssignment.PartyId = Guid.NewGuid();
        cut.Instance.CompleteNextSaveImmediately(Result<Guid>.Success(Guid.NewGuid()));
        Assert.True(await cut.InvokeAsync(view.SaveDraftAsync));
        Assert.Equal(2, cut.Instance.Submissions.Count);
    }

    [Fact]
    public async Task A_late_save_of_a_retired_project_never_resets_the_draft_of_the_project_selected_since()
    {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var first = Project("Retired selection project");
        var second = Project("Current selection project");
        var cut = harness.Context.Render<SavingAssignmentsPage>(parameters => parameters.Add(page => page.TestProjects, [first, second]));
        var view = (ICrmHrAssignmentsWorkspaceView)cut.Instance;
        cut.WaitForAssertion(() => Assert.Equal(first.Id, view.SelectedProjectId));
        Assert.True(await cut.InvokeAsync(view.PrepareRelationshipCreateAsync));
        view.DraftAssignment.PartyId = Guid.NewGuid();
        cut.Instance.HoldSaves();

        var lateSave = cut.InvokeAsync(view.SaveDraftAsync);
        await cut.Instance.WaitForSaveAsync();
        await cut.InvokeAsync(() => view.HandleProjectChanged(second.Id));
        cut.WaitForAssertion(() => Assert.Equal(second.Id, view.SelectedProjectId));
        // The new selection is not blocked by the retired write: its own save is admitted while the first still runs.
        var currentDraft = view.DraftAssignment;
        Assert.Equal(second.Id, currentDraft.ProjectId);
        currentDraft.PartyId = Guid.NewGuid();
        cut.Instance.CompleteNextSaveImmediately(Result<Guid>.Success(Guid.NewGuid()));
        Assert.True(await cut.InvokeAsync(view.SaveDraftAsync));
        Assert.Equal(2, cut.Instance.Submissions.Count);
        Assert.Equal(second.Id, cut.Instance.Submissions[1].ProjectId);

        // The retired write returns: it belongs to the first project and leaves the current draft alone.
        var draftBeforeLateCompletion = view.DraftAssignment;
        draftBeforeLateCompletion.Notes = "still typing for the current project";
        cut.Instance.CompleteSave(Result<Guid>.Success(Guid.NewGuid()));
        await lateSave;

        Assert.Same(draftBeforeLateCompletion, view.DraftAssignment);
        Assert.Equal("still typing for the current project", view.DraftAssignment.Notes);
        Assert.Equal(second.Id, view.SelectedProjectId);
        Assert.Equal(first.Id, cut.Instance.Submissions[0].ProjectId);
        Assert.Equal(first.LifetimeId, cut.Instance.Submissions[0].ExpectedProjectAdmission!.LifetimeId);
    }

    private static ProjectRecordQueryItem Project(string name)
        => new(Guid.NewGuid(), name, ProjectStatus.Active, "Delivery", string.Empty, DateTimeOffset.UtcNow) { LifetimeId = Guid.NewGuid() };

    private sealed class SavingAssignmentsPage : CrmHrAssignmentsPage
    {
        private TaskCompletionSource<Result<Guid>>? heldSave;
        private TaskCompletionSource saveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Result<Guid>? immediateResult;

        [Microsoft.AspNetCore.Components.Parameter]
        public IReadOnlyList<ProjectRecordQueryItem> TestProjects { get; set; } = [];

        public List<ProjectPartyAssignmentUpsertRequest> Submissions { get; } = [];

        public void HoldSaves()
        {
            heldSave = new(TaskCreationOptions.RunContinuationsAsynchronously);
            saveStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        }

        public Task WaitForSaveAsync() => saveStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        public void CompleteSave(Result<Guid> result) => heldSave!.TrySetResult(result);

        public void CompleteNextSaveImmediately(Result<Guid> result) => immediateResult = result;

        protected override Task<Result<Guid>> SaveAssignmentOwnerAsync(ProjectPartyAssignmentUpsertRequest submission, CancellationToken cancellationToken)
        {
            Submissions.Add(submission);
            if (immediateResult is { } immediate)
            {
                immediateResult = null;
                return Task.FromResult(immediate);
            }

            saveStarted.TrySetResult();
            return heldSave!.Task;
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
            => Task.FromResult(new AssignmentSelectionSnapshot(request.RequestedData, [], [], [], []));

        protected override Task<StaffingDashboardModel> GetStaffingDashboardAsync(CancellationToken cancellationToken)
            => Task.FromResult(new StaffingDashboardModel(0, 0m, 0, 0));
    }
}
