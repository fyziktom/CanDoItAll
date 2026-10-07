using Bunit;
using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Resources;
using CanDoItAll.Resources.UI;
using CanDoItAll.Resources.UiSandbox;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Components.ResourcesUi;

public sealed class ResourceBrowseTests {
    [Fact]
    public async Task Removed_source_cleanup_cannot_mark_a_successor_selection_failed() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        var retired = controller.Workspace!;
        store.Sources.RemoveAt(0);
        var gate = store.HoldNext(ResourceScenarioLane.SourceRelease);
        var refresh = controller.RefreshAsync();
        await gate.Entered.Task;
        Assert.Null(controller.Workspace);
        await controller.SelectAsync(store.Sources[0].Key);
        var current = controller.Workspace;
        gate.Release();
        await refresh;
        Assert.Same(current, controller.Workspace);
        Assert.Equal(ResourceViewAccess.Ready, controller.Access);
        Assert.Null(controller.SourceError);
        await retired.DisposeAsync();
        Assert.Equal(1, store.ReleaseCount);
        Assert.False(current!.IsDisposed);
    }

    [Fact]
    public async Task Slow_source_cleanup_is_detached_before_a_new_selection_acquires_its_lease() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        var gate = store.HoldNext(ResourceScenarioLane.SourceRelease);
        var old = controller.SelectAsync(store.Sources[1].Key);
        await gate.Entered.Task;
        await controller.SelectAsync(store.Sources[2].Key);
        var current = controller.Workspace;
        gate.Release();
        await old;
        Assert.Same(current, controller.Workspace);
        Assert.False(current!.IsDisposed);
        Assert.Equal(1, store.ReleaseCount);
    }

    [Fact]
    public async Task Slow_preview_cleanup_cannot_clear_or_dispose_a_new_preview() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        await InvokeAsync(controller, store, 0);
        await controller.SavePromotionAsync();
        var id = Assert.Single(controller.Promotions).Observation!.ResourceId;
        await controller.OpenResourceAsync(id);
        var retired = controller.Preview!;
        var gate = store.HoldNext(ResourceScenarioLane.PreviewRelease);
        var close = controller.ClosePreviewAsync();
        await gate.Entered.Task;
        Assert.Null(controller.Preview);
        await controller.OpenResourceAsync(id);
        var current = controller.Preview;
        gate.Release();
        await close;
        Assert.Same(current, controller.Preview);
        Assert.False(current!.IsDisposed);
        await retired.DisposeAsync();
        Assert.Equal(1, store.ContentReleaseCount);
    }

    [Fact]
    public async Task Held_parent_notification_retains_the_id_and_cannot_steal_a_successor_dialog() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        var gate = new ResourceScenarioGate();
        controller.Promoted = _ => gate.PauseAsync();
        await InvokeAsync(controller, store, 0);
        var pending = controller.SavePromotionAsync();
        await gate.Entered.Task;
        var receipt = Assert.Single(controller.Promotions);
        Assert.True(store.Records.ContainsKey(receipt.Observation!.ResourceId));
        await controller.SelectAsync(store.Sources[1].Key);
        await controller.SelectAsync(store.Sources[0].Key);
        await InvokeAsync(controller, store, 0);
        var current = controller.Workspace;
        var dialog = controller.Promotion;
        gate.Release();
        await pending;
        Assert.Same(current, controller.Workspace);
        Assert.Same(dialog, controller.Promotion);
        Assert.Equal(ResourceEffectState.Committed, receipt.State);
        Assert.Single(store.PromotionCommands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Stale_source_acquired_after_selection_or_retirement_is_released_once(bool dispose) {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = Create(store, context);
        await controller.RefreshAsync();
        var gate = store.HoldNext(ResourceScenarioLane.Source);
        var old = controller.SelectAsync(store.Sources[0].Key);
        await gate.Entered.Task;
        if (dispose) {
            await controller.DisposeAsync();
        } else {
            await controller.SelectAsync(store.Sources[1].Key);
        }
        var current = controller.Workspace;
        gate.Release();
        await old;
        Assert.Same(current, controller.Workspace);
        Assert.Equal(1, store.ReleaseCount);
        Assert.True(dispose || current is { IsDisposed: false });
    }

    [Fact]
    public async Task A_b_a_open_and_old_catalog_completion_cannot_retire_the_latest_a() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = Create(store, context);
        await controller.RefreshAsync();
        await controller.SelectAsync(store.Sources[0].Key);
        var refreshGate = store.HoldNext(ResourceScenarioLane.Catalog);
        var refresh = controller.RefreshAsync();
        await refreshGate.Entered.Task;
        var openGate = store.HoldNext(ResourceScenarioLane.Source);
        var oldB = controller.SelectAsync(store.Sources[1].Key);
        await openGate.Entered.Task;
        await controller.SelectAsync(store.Sources[0].Key);
        var current = controller.Workspace;
        refreshGate.Release();
        openGate.Release();
        await Task.WhenAll(refresh, oldB);
        Assert.Same(current, controller.Workspace);
        Assert.False(current!.IsDisposed);
        Assert.Equal(store.Sources[0].Key, controller.Position!.SourceId);
        Assert.Equal(2, store.ReleaseCount);
    }

    [Fact]
    public async Task Failed_catalog_refresh_retains_current_session_and_reports_stale_data() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = Create(store, context);
        await controller.RefreshAsync();
        await controller.SelectAsync(store.Sources[0].Key);
        var current = controller.Workspace;
        store.FailNextRead = true;
        await controller.RefreshAsync();
        Assert.Same(current, controller.Workspace);
        Assert.NotNull(controller.CatalogError);
        Assert.Equal(ResourceViewAccess.Failed, controller.Access);
        Assert.False(current!.IsDisposed);
    }

    [Fact]
    public async Task Promotion_freezes_all_fields_and_late_completion_does_not_close_successor_dialog() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        await InvokeAsync(controller, store, 0);
        var draft = controller.Promotion!;
        draft.Name = "captured";
        draft.Sensitivity = ResourceSensitivity.Restricted;
        var gate = store.HoldNext(ResourceScenarioLane.Promotion);
        var pending = controller.SavePromotionAsync();
        await gate.Entered.Task;
        await controller.SavePromotionAsync();
        draft.Name = "later";
        draft.ProjectId = store.Projects[1].Id;
        await controller.SelectAsync(store.Sources[1].Key);
        await InvokeAsync(controller, store, 1);
        var next = controller.Promotion;
        gate.Release();
        await pending;
        var command = Assert.Single(store.PromotionCommands);
        Assert.Equal("captured", command.Name);
        Assert.Equal(store.PrimaryProject.Admission, command.Project);
        Assert.Equal(ResourceSensitivity.Restricted, command.Sensitivity);
        Assert.Equal(store.Sources[0], command.Selection.Source);
        Assert.Same(next, controller.Promotion);
        Assert.Equal(store.Sources[1].Key, controller.Workspace!.Source.Key);
        Assert.NotNull(Assert.Single(controller.Promotions).Observation);
    }

    [Fact]
    public async Task A_b_a_after_promotion_dispatch_does_not_reopen_the_new_a_session() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        await InvokeAsync(controller, store, 0);
        var gate = store.HoldNext(ResourceScenarioLane.Promotion);
        var pending = controller.SavePromotionAsync();
        await controller.SelectAsync(store.Sources[1].Key);
        await controller.SelectAsync(store.Sources[0].Key);
        var current = controller.Workspace;
        gate.Release();
        await pending;
        Assert.Same(current, controller.Workspace);
        Assert.False(current!.IsDisposed);
    }

    [Fact]
    public async Task Confirmed_promotion_identity_survives_parent_callback_failure() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        controller.Promoted = _ => throw new IOException("Parent reference refresh failed.");
        await InvokeAsync(controller, store, 0);
        await controller.SavePromotionAsync();
        var receipt = Assert.Single(controller.Promotions);
        Assert.Equal(ResourceEffectState.CommittedWarning, receipt.State);
        Assert.True(store.Records.ContainsKey(receipt.Observation!.ResourceId));
        Assert.Null(controller.Promotion);
        await controller.RefreshAsync();
        Assert.Single(store.PromotionCommands);
    }

    [Theory]
    [InlineData(ResourceScenario.RefusedWrite, ResourceEffectState.Refused, false)]
    [InlineData(ResourceScenario.UnknownWrite, ResourceEffectState.Unknown, false)]
    [InlineData(ResourceScenario.CommittedWarning, ResourceEffectState.CommittedWarning, true)]
    public async Task Promotion_outcomes_distinguish_refusal_unknown_and_confirmed_warning(ResourceScenario scenario, ResourceEffectState state, bool known) {
        var store = new ResourceScenarioStore(scenario);
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        await InvokeAsync(controller, store, 0);
        await controller.SavePromotionAsync();
        var receipt = Assert.Single(controller.Promotions);
        Assert.Equal(state, receipt.State);
        Assert.Equal(known, receipt.Observation is not null);
        if (known) {
            Assert.Null(receipt.Observation!.ScopeRevision);
        }
        if (state == ResourceEffectState.Unknown) {
            await controller.SavePromotionAsync();
            Assert.Single(store.PromotionCommands);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_preview_is_released_without_mounting_under_successor(bool dispose) {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        await InvokeAsync(controller, store, 0);
        await controller.SavePromotionAsync();
        var id = Assert.Single(controller.Promotions).Observation!.ResourceId;
        var gate = store.HoldNext(ResourceScenarioLane.Preview);
        var preview = controller.OpenResourceAsync(id);
        await gate.Entered.Task;
        if (dispose) {
            await controller.DisposeAsync();
        } else {
            await controller.SelectAsync(store.Sources[1].Key);
        }
        gate.Release();
        await preview;
        Assert.Null(controller.Preview);
        Assert.Equal(1, store.ContentReleaseCount);
    }

    [Fact]
    public async Task Real_readonly_content_is_released_once_and_failed_cleanup_is_reported() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        await InvokeAsync(controller, store, 0);
        await controller.SavePromotionAsync();
        await controller.OpenResourceAsync(Assert.Single(controller.Promotions).Observation!.ResourceId);
        var preview = controller.Preview!;
        Assert.Equal(FileInteractionMode.View, preview.Request.Mode);
        await using (var content = await preview.ContentSource.OpenReadAsync(new(preview.Request.File))) {
            using var reader = new StreamReader(content.Stream);
            Assert.Equal(ResourceScenarioStore.FixtureText, await reader.ReadToEndAsync());
        }
        store.FailCleanup = true;
        await controller.ClosePreviewAsync();
        await preview.DisposeAsync();
        Assert.Equal(1, store.ContentReleaseCount);
        Assert.Null(controller.Preview);
        Assert.NotNull(controller.CleanupError);
    }

    [Theory]
    [InlineData(true, FileBrowserInvocationKind.PointerDoubleClick, false)]
    [InlineData(false, FileBrowserInvocationKind.PointerDoubleClick, true)]
    [InlineData(false, FileBrowserInvocationKind.Keyboard, false)]
    public async Task Activation_policy_keeps_supported_and_keyboard_invocations_in_promotion(bool internallySupported, FileBrowserInvocationKind kind, bool launched) {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        var item = store.Selection(store.Sources[0].Key, 1).Item;
        await controller.ActivateAsync(new(item, kind), internallySupported);
        Assert.Equal(launched ? 1 : 0, store.Launches.Count);
        Assert.Equal(!launched, controller.Promotion is not null);
    }

    [Fact]
    public async Task Already_admitted_external_action_keeps_truthful_original_receipt_after_navigation() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var controller = await OpenAsync(store, context);
        var item = store.Selection(store.Sources[0].Key, 1).Item;
        var gate = store.HoldNext(ResourceScenarioLane.FileAction);
        var pending = controller.ActivateAsync(new(item, FileBrowserInvocationKind.PointerDoubleClick), false);
        await gate.Entered.Task;
        await controller.SelectAsync(store.Sources[1].Key);
        gate.Release();
        await pending;
        var receipt = Assert.Single(controller.Actions);
        Assert.Equal(ResourceEffectState.Committed, receipt.State);
        Assert.Equal(store.Sources[0], receipt.Selection.Source);
        Assert.Equal(store.Sources[1], controller.Workspace!.Source);
        Assert.Single(store.Launches);
    }

    [Fact]
    public async Task Queued_download_does_not_open_content_after_authorization_returns_to_retired_host() {
        var store = new ResourceScenarioStore();
        using var context = Context();
        await using var runner = new FileToolsHostActionRunner(context.JSInterop.JSRuntime);
        using var lifetime = new CancellationTokenSource();
        var selection = store.Selection(store.Sources[0].Key);
        var gate = store.HoldNext(ResourceScenarioLane.Download);
        var pending = runner.ExecuteAsync(FileToolsHostAction.Download,
            (_, _) => throw new InvalidOperationException("No local launch expected."),
            token => store.AuthorizeDownloadAsync(selection, token), lifetime.Token).AsTask();
        await gate.Entered.Task;
        lifetime.Cancel();
        gate.Release();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(1, store.DownloadReleaseCount);
        Assert.Empty(context.JSInterop.Invocations);
    }

    [Fact]
    public async Task Queued_download_does_not_trigger_javascript_after_module_import_returns_to_retired_host() {
        var store = new ResourceScenarioStore();
        var runtime = new HeldJsRuntime();
        await using var runner = new FileToolsHostActionRunner(runtime);
        using var lifetime = new CancellationTokenSource();
        var selection = store.Selection(store.Sources[0].Key);
        var pending = runner.ExecuteAsync(FileToolsHostAction.Download,
            (_, _) => throw new InvalidOperationException("No local launch expected."),
            token => store.AuthorizeDownloadAsync(selection, token), lifetime.Token).AsTask();
        Assert.Equal(1, runtime.Calls);
        lifetime.Cancel();
        var module = new RecordingJsModule();
        runtime.Module.SetResult(module);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
        Assert.Equal(0, module.Calls);
        Assert.Equal(1, module.Disposals);
        Assert.Equal(1, store.DownloadReleaseCount);
    }

    internal static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }
    internal static ResourceBrowseController Create(IResourceBrowseOwner store, BunitContext context) => new(store, new(context.JSInterop.JSRuntime));
    internal static async Task<ResourceBrowseController> OpenAsync(ResourceScenarioStore store, BunitContext context) {
        var controller = Create(store, context);
        await controller.RefreshAsync();
        await controller.SelectAsync(store.Sources[0].Key);
        return controller;
    }
    internal static Task InvokeAsync(ResourceBrowseController controller, ResourceScenarioStore store, int source) =>
        controller.ActivateAsync(new(store.Selection(store.Sources[source].Key).Item, FileBrowserInvocationKind.Keyboard), true);
    private sealed class RecordingJsModule : IJSObjectReference {
        public int Calls { get; private set; }
        public int Disposals { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            Calls++;
            return ValueTask.FromResult(default(TValue)!);
        }
        public ValueTask DisposeAsync() {
            Disposals++;
            return ValueTask.CompletedTask;
        }
    }
    private sealed class HeldJsRuntime : IJSRuntime {
        public TaskCompletionSource<IJSObjectReference> Module { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Calls { get; private set; }
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) => InvokeAsync<TValue>(identifier, CancellationToken.None, args);
        public async ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args) {
            Assert.Equal("import", identifier);
            Calls++;
            return (TValue)(object)await Module.Task;
        }
    }
}
