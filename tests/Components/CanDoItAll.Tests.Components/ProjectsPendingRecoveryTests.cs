using Bunit;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectsPendingRecoveryTests {
    [Fact]
    public async Task Accepted_storage_failure_blocks_fresh_save_then_recovery_preserves_historical_and_current_values() {
        var store = new ProjectPendingSubmissionTestStore { AcceptFailure = new IOException("Injected browser acceptance write failure") };
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProjectPendingSubmissionStore>(store));
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var created = await projects.SaveAsync(new() { Name = "Initial definition" });
        Assert.True(created.IsSuccess);
        var cut = Render(harness);
        cut.WaitForElement("[data-testid='project-card']");
        await cut.InvokeAsync(() => cut.FindComponent<ProjectsBoard>().Instance.OpenProjectPreview.InvokeAsync(created.Value));
        var modal = cut.FindComponent<ProjectModalHost>();
        await cut.InvokeAsync(() => modal.Instance.SwitchToEditor.InvokeAsync());
        var draft = modal.Instance.Draft;
        draft.Model.Name = "Accepted historical definition";
        await cut.InvokeAsync(() => modal.Instance.Save.InvokeAsync(draft.Capture()));
        var operation = Assert.Single(store.Records).Operation;
        Assert.Equal(ProjectEditorMutationState.SaveReconciliationFailed, draft.Mutation);
        Assert.Equal("Accepted historical definition", (await projects.GetAsync(created.Value)).Name);
        Assert.NotNull(draft.AcceptedOutcome);
        Assert.Null(draft.Acknowledgement);
        await cut.InvokeAsync(() => modal.Instance.Save.InvokeAsync(draft.Capture()));
        Assert.Equal(1, store.DispatchClaims);

        var later = await projects.GetAsync(created.Value);
        later.Name = "Current saved later";
        Assert.True((await projects.SaveAsync(later)).IsSuccess);
        draft.Model.Name = "Unsaved typing after acceptance";
        draft.EditContext.NotifyFieldChanged(new(draft.Model, nameof(draft.Model.Name)));
        store.AcceptFailure = null;
        await cut.InvokeAsync(() => modal.Instance.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.Check)));
        await cut.InvokeAsync(() => modal.Instance.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.ApplyAccepted)));
        Assert.Equal("Unsaved typing after acceptance", draft.Model.Name);
        var historical = draft.Model.ExpectedDefinition;
        await cut.InvokeAsync(() => modal.Instance.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.ReadCurrent)));
        Assert.Equal("Current saved later", draft.InspectedModel!.Name);
        Assert.Same(historical, draft.Model.ExpectedDefinition);
        Assert.NotEqual(draft.InspectedDefinition!.Expectation.Revision, historical!.Revision);
        await cut.InvokeAsync(() => modal.Instance.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.ClearResolved)));
        Assert.Empty(store.Records);
        Assert.True(draft.CanMutate);
        Assert.Equal(1, store.DispatchClaims);
    }

    [Fact]
    public async Task Close_during_accepted_persistence_preserves_successor_and_requires_explicit_recovery() {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new ProjectPendingSubmissionTestStore {
            BeforeAccept = () => {
                started.TrySetResult();
                return release.Task;
            }
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProjectPendingSubmissionStore>(store));
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var first = (await projects.SaveAsync(new() { Name = "Pending target" })).Value;
        var second = (await projects.SaveAsync(new() { Name = "Successor target" })).Value;
        var cut = Render(harness);
        cut.WaitForElement("[data-testid='project-card']");
        await cut.InvokeAsync(() => cut.FindComponent<ProjectsBoard>().Instance.OpenProjectPreview.InvokeAsync(first));
        var modal = cut.FindComponent<ProjectModalHost>();
        var original = modal.Instance.Draft;
        original.Model.Name = "Accepted while closing";
        var save = cut.InvokeAsync(() => modal.Instance.Save.InvokeAsync(original.Capture()));
        try {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            await cut.InvokeAsync(() => modal.Instance.Close.InvokeAsync());
            await cut.InvokeAsync(() => cut.FindComponent<ProjectsBoard>().Instance.OpenProjectPreview.InvokeAsync(second));
            var successor = cut.FindComponent<ProjectModalHost>().Instance.Draft;
            release.TrySetResult();
            await save.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Same(successor, cut.FindComponent<ProjectModalHost>().Instance.Draft);
            Assert.Equal("Successor target", successor.Model.Name);
            Assert.Null(original.Acknowledgement);
            Assert.Equal(ProjectPendingStage.Accepted, Assert.Single(store.Records).Stage);
            Assert.Equal("Accepted while closing", (await projects.GetAsync(first)).Name);
            Assert.Equal(1, store.DispatchClaims);
        } finally {
            release.TrySetResult();
            await save.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Undispatched_cancellation_is_explicit_but_uncertain_attempt_cannot_be_canceled(bool dispatched) {
        var store = new ProjectPendingSubmissionTestStore();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProjectPendingSubmissionStore>(store));
        var native = harness.Context.Services.GetRequiredService<ProjectNativeDefinitionOperations>();
        var acquired = await native.AcquireEditorAsync(null);
        acquired.Model.Name = "Frozen creation";
        var draft = new ProjectEditorDraft(acquired.Model, acquired.Scope);
        var frozen = native.Freeze(draft.Capture(), acquired.Scope, await native.GetContextAsync());
        var prepared = await store.PrepareAsync(frozen, false);
        if (dispatched) {
            var attempt = Guid.NewGuid();
            await store.BeginDispatchAsync(prepared.Record!, attempt, false);
            await store.ObserveAttemptAsync(frozen.Submission.Operation, attempt, ProjectPendingAttemptOutcome.Unconfirmed);
        }
        var cut = Render(harness);
        cut.WaitForElement("[data-testid='project-pending-open']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-pending-open']").ClickAsync(new()));
        var modal = cut.FindComponent<ProjectModalHost>();
        Assert.Equal(!dispatched, modal.Instance.PendingRecovery!.CanCancel);
        await cut.InvokeAsync(() => modal.Instance.PendingAction.InvokeAsync(new(frozen.Submission.Operation, ProjectPendingEditorAction.CancelPrepared)));
        Assert.Equal(dispatched ? 1 : 0, store.Records.Count);
        Assert.Equal(!dispatched, modal.Instance.Draft.CanMutate);
        Assert.Equal("Frozen creation", modal.Instance.Draft.Model.Name);
        Assert.Empty(await harness.Context.Services.GetRequiredService<ProjectsService>().ListAsync());
    }

    [Fact]
    public async Task Foreign_reference_can_be_discarded_when_scoped_inventory_is_unavailable_without_disclosing_its_body() {
        var store = new ProjectPendingSubmissionTestStore { ScopedListFailure = new IOException("Current scoped inventory unavailable") };
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProjectPendingSubmissionStore>(store));
        var codec = harness.Context.Services.GetRequiredService<IProjectDefinitionRecoveryCodec>();
        var operation = new ProjectDefinitionOperation(new(Guid.NewGuid()), new(1), Guid.NewGuid(), new("pc1:" + new string('b', 64)));
        var frozen = codec.Freeze(new ProjectDefinitionSubmission.Create(new(operation, ProjectDefinitionRequestVersion.VersionOne,
            new(new("Private foreign draft body", "", "", ProjectStatus.Active, "", null), [], [], null))), ProjectDefinitionLimits.VersionOneMaximum);
        await store.PrepareAsync(frozen, false);
        var cut = Render(harness, waitForSubmission: false);
        cut.WaitForAssertion(() => Assert.Contains("Retained saves could not be read safely", cut.Markup));
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-reference-manage']").ClickAsync(new()));
        Assert.Contains("current context unavailable", cut.Markup);
        Assert.DoesNotContain("Private foreign draft body", cut.Markup);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-reference-select']").ClickAsync(new()));
        Assert.Single(store.Records);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-reference-confirm']").ClickAsync(new()));
        Assert.Empty(store.Records);
        Assert.Equal(0, store.DispatchClaims);
        Assert.Empty(await harness.Context.Services.GetRequiredService<ProjectsService>().ListAsync());
    }

    [Fact]
    public async Task Discard_during_late_acceptance_preserves_known_result_and_leaves_the_original_draft_terminal() {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new ProjectPendingSubmissionTestStore {
            BeforeAccept = () => {
                started.TrySetResult();
                return release.Task;
            }
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProjectPendingSubmissionStore>(store));
        var cut = Render(harness);
        await cut.InvokeAsync(() => cut.Find("[data-testid='projects-new-button']").ClickAsync(new()));
        var modal = cut.FindComponent<ProjectModalHost>();
        var draft = modal.Instance.Draft;
        draft.Model.Name = "Accepted before local discard";
        var save = cut.InvokeAsync(() => modal.Instance.Save.InvokeAsync(draft.Capture()));
        try {
            await started.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var operation = Assert.Single(store.Records).Operation;
            await cut.InvokeAsync(() => cut.Find("[data-testid='project-reference-manage']").ClickAsync(new()));
            await cut.InvokeAsync(() => cut.Find("[data-testid='project-reference-select']").ClickAsync(new()));
            await cut.InvokeAsync(() => cut.Find("[data-testid='project-reference-confirm']").ClickAsync(new()));
            Assert.Equal(ProjectEditorMutationState.LocalRecoveryDiscarded, draft.Mutation);
            release.TrySetResult();
            await save.WaitAsync(TimeSpan.FromSeconds(30));
            Assert.Equal(ProjectEditorMutationState.LocalRecoveryDiscarded, draft.Mutation);
            Assert.NotNull(draft.AcceptedOutcome);
            Assert.Contains(operation.Id.ToString("D"), draft.Message);
            Assert.Null(draft.Acknowledgement);
            await cut.InvokeAsync(() => modal.Instance.Save.InvokeAsync(draft.Capture()));
            Assert.Equal(1, store.DispatchClaims);
            Assert.Empty(store.Records);
            Assert.Equal("Accepted before local discard", Assert.Single(await harness.Context.Services.GetRequiredService<ProjectsService>().ListAsync()).Name);
        } finally {
            release.TrySetResult();
            await save.WaitAsync(TimeSpan.FromSeconds(30));
        }
    }

    private static IRenderedComponent<ProjectsPage> Render(ComponentTestHarness harness, bool waitForSubmission = true) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/projects");
        var cut = harness.Context.Render<ProjectsPage>();
        if (waitForSubmission) {
            cut.WaitForAssertion(() => Assert.True(cut.FindComponent<ProjectModalHost>().Instance.SubmissionReady));
        }
        return cut;
    }
}
