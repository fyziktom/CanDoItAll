using System.Runtime.ExceptionServices;
using Bunit;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectsLifecycleRecoveryTests {
    [Fact]
    public async Task Late_committed_deletion_preserves_a_newer_editor_and_retains_the_original_receipt() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new ProjectLifecyclePendingTestStore {
            BeforeAccept = () => {
                entered.TrySetResult();
                return release.Task;
            }
        };
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProjectLifecyclePendingStore>(store));
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var first = await projects.SaveAsync(new() { Name = "Original deletion target" });
        var second = await projects.SaveAsync(new() { Name = "Successor target" });
        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        var cut = Render(harness);
        var modal = await OpenAsync(cut, first.Value);
        var original = modal.Instance.Draft;
        var expected = original.Model.ExpectedDefinition;
        Assert.NotNull(expected);
        original.Model.Name = "Unsubmitted original typing";
        var deletion = cut.InvokeAsync(() => modal.Instance.Delete.InvokeAsync());
        Exception? primary = null;
        var observed = false;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            var captured = Assert.Single(store.Records);
            Assert.Equal(expected, Assert.IsType<ProjectLifecycleCommand.Delete>(captured.Frozen.Submission.Command).Expected);
            Assert.Equal(ProjectPendingStage.Dispatching, captured.Stage);
            Assert.DoesNotContain(await projects.ListAsync(), project => project.Id == first.Value);
            await cut.InvokeAsync(() => modal.Instance.Close.InvokeAsync());
            var currentModal = await OpenAsync(cut, second.Value);
            var successor = currentModal.Instance.Draft;
            var successorExpected = successor.Model.ExpectedDefinition;
            var successorContext = successor.EditContext;
            successor.Model.Name = "Later unsaved successor typing";
            successor.EditContext.NotifyFieldChanged(new(successor.Model, nameof(successor.Model.Name)));
            release.TrySetResult();
            try {
                await deletion;
            } finally {
                observed = deletion.IsCompleted;
            }
            Assert.Same(successor, cut.FindComponent<ProjectModalHost>().Instance.Draft);
            Assert.Same(successorContext, successor.EditContext);
            Assert.Equal(successorExpected, successor.Model.ExpectedDefinition);
            Assert.Equal("Later unsaved successor typing", successor.Model.Name);
            Assert.Equal("Unsubmitted original typing", original.Model.Name);
            Assert.True(cut.FindComponent<ProjectModalHost>().Instance.IsOpen);
            var retained = Assert.Single(store.Records);
            Assert.Equal(captured.Frozen.Payload, retained.Frozen.Payload);
            Assert.Equal(ProjectPendingStage.Accepted, retained.Stage);
            var receipt = retained.Accepted!.Accepted;
            Assert.IsType<ProjectLifecycleAcknowledgement.DeletionCommitted>(receipt.Acknowledgement);
            Assert.Equal(expected, Assert.IsType<ProjectLifecycleCommand.Delete>(receipt.Original.Command).Expected);
            var native = harness.Context.Services.GetRequiredService<ProjectNativeLifecycleOperations>();
            var found = Assert.IsType<ProjectLifecycleLookup.Found>(await native.LookupAsync(retained.Operation));
            var codec = harness.Context.Services.GetRequiredService<IProjectLifecycleRecoveryCodec>();
            Assert.Equal(retained.Accepted.Payload, codec.EncodeAccepted(retained.Frozen.Submission, found.Accepted).Payload);
            var surface = cut.FindComponent<ProjectLifecyclePendingSurface>();
            Assert.False(surface.Instance.View!.CanApply);
            Assert.False(surface.Instance.View.CanRetry);
            await cut.InvokeAsync(() => surface.Instance.Action.InvokeAsync(new(retained.Operation, ProjectLifecyclePendingAction.ApplyAccepted)));
            await cut.InvokeAsync(() => surface.Instance.Action.InvokeAsync(new(retained.Operation, ProjectLifecyclePendingAction.RetryOriginal)));
            Assert.Same(successor, cut.FindComponent<ProjectModalHost>().Instance.Draft);
            Assert.Equal(1, store.DispatchClaims);
        } catch (Exception failure) {
            primary = failure;
        } finally {
            release.TrySetResult();
            await DrainAsync(deletion, observed, primary);
        }
    }

    [Fact]
    public async Task Accepted_storage_failure_holds_the_original_editor_until_exact_owner_lookup_repairs_evidence() {
        var store = new ProjectLifecyclePendingTestStore { AcceptFailure = new IOException("Injected lifecycle evidence failure") };
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.AddSingleton<IProjectLifecyclePendingStore>(store));
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var created = await projects.SaveAsync(new() { Name = "Deletion evidence target" });
        Assert.True(created.IsSuccess);
        var cut = Render(harness);
        var modal = await OpenAsync(cut, created.Value);
        var original = modal.Instance.Draft;
        var expected = original.Model.ExpectedDefinition;
        var editContext = original.EditContext;
        original.Model.Name = "Keep this unsaved name";
        await cut.InvokeAsync(() => modal.Instance.Delete.InvokeAsync());
        var retained = Assert.Single(store.Records);
        Assert.Equal(expected, Assert.IsType<ProjectLifecycleCommand.Delete>(retained.Frozen.Submission.Command).Expected);
        Assert.DoesNotContain(await projects.ListAsync(), project => project.Id == created.Value);
        Assert.Equal(ProjectEditorMutationState.AcceptedReadOnly, original.Mutation);
        Assert.False(original.CanMutate);
        Assert.Same(editContext, original.EditContext);
        Assert.Equal("Keep this unsaved name", original.Model.Name);
        var surface = cut.FindComponent<ProjectLifecyclePendingSurface>();
        Assert.Contains("was accepted", surface.Instance.View!.Message);
        Assert.False(surface.Instance.View.CanApply);
        Assert.False(surface.Instance.View.CanClear);
        await cut.InvokeAsync(() => modal.Instance.Delete.InvokeAsync());
        Assert.Equal(1, store.DispatchClaims);

        store.AcceptFailure = null;
        await cut.InvokeAsync(() => surface.Instance.Action.InvokeAsync(new(retained.Operation, ProjectLifecyclePendingAction.Check)));
        var repaired = Assert.Single(store.Records);
        Assert.Equal(ProjectPendingStage.Accepted, repaired.Stage);
        Assert.Equal(retained.Frozen.Payload, repaired.Frozen.Payload);
        Assert.True(cut.FindComponent<ProjectLifecyclePendingSurface>().Instance.View!.CanApply);
        Assert.Equal(expected, original.Model.ExpectedDefinition);
        await cut.InvokeAsync(() => surface.Instance.Action.InvokeAsync(new(retained.Operation, ProjectLifecyclePendingAction.ApplyAccepted)));
        Assert.Equal(ProjectEditorMutationState.Deleted, original.Mutation);
        Assert.False(cut.FindComponent<ProjectModalHost>().Instance.IsOpen);
        Assert.Contains("Cleanup completion is tracked separately", cut.Markup);
        Assert.Equal("Keep this unsaved name", original.Model.Name);
        Assert.Equal(1, store.DispatchClaims);
        await cut.InvokeAsync(() => surface.Instance.Action.InvokeAsync(new(retained.Operation, ProjectLifecyclePendingAction.ClearResolved)));
        Assert.Empty(store.Records);
        var native = harness.Context.Services.GetRequiredService<ProjectNativeLifecycleOperations>();
        var found = Assert.IsType<ProjectLifecycleLookup.Found>(await native.LookupAsync(retained.Operation));
        var codec = harness.Context.Services.GetRequiredService<IProjectLifecycleRecoveryCodec>();
        Assert.Equal(repaired.Accepted!.Payload, codec.EncodeAccepted(repaired.Frozen.Submission, found.Accepted).Payload);
    }

    private static IRenderedComponent<ProjectsPage> Render(ComponentTestHarness harness) {
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/projects");
        var cut = harness.Context.Render<ProjectsPage>();
        cut.WaitForAssertion(() => Assert.False(cut.FindComponent<ProjectLifecyclePendingSurface>().Instance.Loading));
        return cut;
    }

    private static async Task<IRenderedComponent<ProjectModalHost>> OpenAsync(IRenderedComponent<ProjectsPage> cut, Guid id) {
        cut.WaitForElement("[data-testid='project-card']");
        await cut.InvokeAsync(() => cut.FindComponent<ProjectsBoard>().Instance.OpenProjectPreview.InvokeAsync(id));
        var modal = cut.FindComponent<ProjectModalHost>();
        cut.WaitForAssertion(() => Assert.True(modal.Instance.SubmissionReady));
        return modal;
    }

    private static async Task DrainAsync(Task operation, bool alreadyObserved, Exception? primary) {
        Exception? terminal = null;
        try {
            await operation;
        } catch (Exception failure) {
            if (!alreadyObserved) {
                terminal = failure;
            }
        }
        if (primary is not null && terminal is not null && !ReferenceEquals(primary, terminal)) {
            throw new AggregateException(primary, terminal);
        }
        if (primary is not null || terminal is not null) {
            ExceptionDispatchInfo.Capture(primary ?? terminal!).Throw();
        }
    }
}
