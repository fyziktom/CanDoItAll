using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.UI;
using CanDoItAll.Memory.UiSandbox;
using CanDoItAll.Modules.Memory.Pages;
using CanDoItAll.Modules.Memory.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.MemoryUi;

public sealed class MemoryWorkspaceTests {
    [Fact]
    public async Task Refresh_and_tabs_preserve_actual_unblurred_controls_and_nested_drafts() {
        var store = new MemoryScenarioStore();
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        using var context = Context();
        var cut = context.Render<MemoryWorkspaceSurface>(p => p.Add(c => c.Controller, workspace));
        await cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-editor-display-name']").Input("unfinished name"));
        workspace.Editor.Http.BaseUrl = "https://unfinished/";
        workspace.Editor.Mcp.RemoteEndpoint = "https://other/";
        workspace.FeedbackEditor.Comment = "feedback draft";
        workspace.IngestionEditor.ContentText = "source draft";
        var draft = workspace.Draft;
        await cut.InvokeAsync(workspace.RefreshAsync);
        await cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-tab-query']").Click());
        await cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-query-text']").Input("unblurred query"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-tab-providers']").Click());
        Assert.Same(draft, workspace.Draft);
        Assert.Equal("unfinished name", workspace.Editor.DisplayName);
        Assert.Equal("unfinished name", cut.Find("[data-testid='memory-ui-editor-display-name']").GetAttribute("value"));
        Assert.Equal("https://unfinished/", workspace.Editor.Http.BaseUrl);
        Assert.Equal("https://other/", workspace.Editor.Mcp.RemoteEndpoint);
        Assert.Equal("unblurred query", workspace.QueryEditor.Query);
        Assert.Equal("feedback draft", workspace.FeedbackEditor.Comment);
        Assert.Equal("source draft", workspace.IngestionEditor.ContentText);
    }

    [Fact]
    public async Task Seven_tabs_use_real_children_and_provider_UI_is_lazy() {
        using var workspace = new MemoryProvidersPageController(new MemoryScenarioStore(MemoryScenario.UiVariants));
        await workspace.RefreshAsync();
        using var context = Context();
        var cut = context.Render<MemoryWorkspaceSurface>(p => p.Add(c => c.Controller, workspace));
        Assert.Equal(7, cut.FindAll("[role='tab']").Count);
        Assert.Empty(cut.FindAll("[data-testid='memory-ui-provider-rcl-host']"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-tab-provider-ui']").Click());
        Assert.Single(cut.FindAll("[data-testid='memory-ui-provider-rcl-host']"));
        Assert.Single(cut.FindAll("iframe"));
        Assert.Equal("allow-scripts allow-forms allow-same-origin", cut.Find("iframe").GetAttribute("sandbox"));
        Assert.Equal("no-referrer", cut.Find("iframe").GetAttribute("referrerpolicy"));
        Assert.Equal("noopener noreferrer", cut.Find("[data-testid='memory-ui-provider-external-link']").GetAttribute("rel"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-tab-providers']").Click());
        Assert.Empty(cut.FindAll("iframe"));
    }

    [Fact]
    public async Task Save_captures_destination_nested_inputs_and_preserves_later_edits_on_readback_failure() {
        var store = new MemoryScenarioStore();
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        workspace.Editor.InstanceId = "provider.destination";
        workspace.Editor.Http.BaseUrl = "https://captured/";
        workspace.Editor.SelectionTags.Add("captured");
        store.HoldNext(MemoryScenarioLane.Save);
        var save = workspace.SaveProviderAsync();
        Assert.Equal(1, store.PendingCount);
        workspace.Editor.InstanceId = "provider.later";
        workspace.Editor.Http.BaseUrl = "https://later/";
        workspace.Editor.SelectionTags.Clear();
        store.FailNextRead = true;
        store.ReleaseAll();
        await save.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(MemoryEffectState.ReadbackWarning, Assert.Single(workspace.Submissions).State);
        Assert.Equal("https://captured/", store.Profiles["provider.destination"].Http.BaseUrl);
        Assert.Contains("captured", store.Profiles["provider.destination"].SelectionTags);
        Assert.True(store.Profiles.ContainsKey(MemoryScenarioStore.ProviderA));
        Assert.Equal("provider.later", workspace.Editor.InstanceId);
        await workspace.RefreshAsync();
        Assert.Equal(1, store.WriteCount);
    }

    [Fact]
    public async Task Duplicate_save_query_and_status_are_admitted_once_per_destination() {
        var store = new MemoryScenarioStore(MemoryScenario.AcceptedQuery);
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        store.HoldNext(MemoryScenarioLane.Save);
        var save = workspace.SaveProviderAsync();
        await workspace.SaveProviderAsync();
        await workspace.RunQueryAsync();
        Assert.Equal(1, store.PendingCount);
        Assert.Equal(0, store.QueryCount);
        store.ReleaseAll();
        await save.WaitAsync(TimeSpan.FromSeconds(5));
        workspace.QueryEditor.UseAsyncQuery = true;
        await workspace.RunQueryAsync();
        var operation = Assert.Single(workspace.Snapshot!.Operations).OperationId;
        store.HoldNext(MemoryScenarioLane.Status);
        var status = workspace.RefreshOperationAsync(operation);
        await workspace.RefreshOperationAsync(operation);
        Assert.Equal(1, store.StatusCount);
        store.ReleaseAll();
        await status.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(MemoryLedgerStatus.Completed, workspace.OperationActionResult!.Operation!.Status);
    }

    [Fact]
    public async Task Query_captures_all_fields_and_late_result_cannot_patch_A_B_A_or_force_a_tab() {
        var store = new MemoryScenarioStore();
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        workspace.QueryEditor.Query = "original";
        workspace.QueryEditor.SourceModule = "module";
        workspace.QueryEditor.SourceRecordId = "record";
        workspace.QueryEditor.Citation = "citation";
        store.HoldNext(MemoryScenarioLane.Query);
        var query = workspace.RunQueryAsync();
        workspace.QueryEditor.Query = "changed";
        workspace.QueryEditor.UseAsyncQuery = true;
        await workspace.SelectProviderAsync(MemoryScenarioStore.ProviderB);
        workspace.QueryEditor.Query = "independent";
        await workspace.RunQueryAsync();
        Assert.NotNull(workspace.QueryResult);
        await workspace.SelectProviderAsync(MemoryScenarioStore.ProviderA);
        workspace.ActiveTabIndex = 2;
        await workspace.RunQueryAsync();
        Assert.Equal(2, store.QueryCount);
        store.ReleaseAll();
        await query.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(workspace.QueryResult);
        Assert.Equal(2, workspace.ActiveTabIndex);
        var original = workspace.Submissions[0].QueryResult!;
        Assert.Contains("original", original.ContextPack!.Summary, StringComparison.Ordinal);
        Assert.Equal("module", original.ContextPack.Sections[0].Text);
        Assert.Equal("record", original.ContextPack.Sections[0].Citations[0].SourceRef);
        Assert.Equal("citation", original.ContextPack.Sections[0].Citations[0].Label);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_read_success_or_error_cannot_replace_successor_or_clear_loading(bool fail) {
        var store = new MemoryScenarioStore();
        var owner = new DelayedReads(store);
        using var workspace = new MemoryProvidersPageController(owner);
        await workspace.RefreshAsync();
        var old = owner.Hold();
        var first = workspace.RefreshAsync();
        await workspace.SelectProviderAsync(MemoryScenarioStore.ProviderB);
        await workspace.SelectProviderAsync(MemoryScenarioStore.ProviderA);
        workspace.Editor.DisplayName = "current";
        var newer = owner.Hold();
        var second = workspace.RefreshAsync();
        if (fail) {
            old.SetException(new InvalidOperationException("SECRET-OLD-ERROR"));
        } else {
            old.SetResult(await store.GetSnapshotAsync(MemoryScenarioStore.ProviderB));
        }
        await first.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.True(workspace.IsLoading);
        Assert.Equal("current", workspace.Editor.DisplayName);
        Assert.Null(workspace.ErrorMessage);
        newer.SetResult(await store.GetSnapshotAsync(MemoryScenarioStore.ProviderA));
        await second.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(workspace.IsLoading);
        Assert.Equal(MemoryScenarioStore.ProviderA, workspace.SelectedProviderId);
    }

    [Fact]
    public async Task Missing_exact_selection_does_not_borrow_first_provider_and_remaining_list_is_selectable() {
        var store = new MemoryScenarioStore();
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        workspace.Editor.DisplayName = "retained missing draft";
        store.Remove(MemoryScenarioStore.ProviderA);
        await workspace.RefreshAsync();
        Assert.Null(workspace.SelectedProvider);
        Assert.Equal(MemoryScenarioStore.ProviderA, workspace.SelectedProviderId);
        Assert.Equal("retained missing draft", workspace.Editor.DisplayName);
        await workspace.RunQueryAsync();
        Assert.Equal(0, store.QueryCount);
        Assert.NotEmpty(workspace.Snapshot!.Providers);
        await workspace.SelectProviderAsync(MemoryScenarioStore.ProviderB);
        Assert.NotNull(workspace.SelectedProvider);
    }

    [Fact]
    public async Task Unknown_save_is_locked_and_exact_review_control_serializes_recovery() {
        var store = new MemoryScenarioStore { FailAfterSave = true };
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        await workspace.SaveProviderAsync();
        var receipt = Assert.Single(workspace.Submissions);
        Assert.Equal(MemoryEffectState.Unknown, receipt.State);
        await workspace.SaveProviderAsync();
        Assert.Equal(1, store.WriteCount);
        using var context = Context();
        var cut = context.Render<MemoryWorkspaceSurface>(p => p.Add(c => c.Controller, workspace));
        store.HoldNext(MemoryScenarioLane.Read);
        var review = cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-review']").ClickAsync(new()));
        cut.WaitForAssertion(() => Assert.True(cut.Find("[data-testid='memory-ui-review']").HasAttribute("disabled")));
        var reads = store.ReadCount;
        await workspace.ReviewAsync(receipt);
        Assert.Equal(reads, store.ReadCount);
        store.ReleaseAll();
        await review.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(MemoryEffectState.Reviewed, receipt.State);
        store.HoldNext(MemoryScenarioLane.Save);
        var successor = workspace.SaveProviderAsync();
        await workspace.ReviewAsync(receipt);
        Assert.Equal(MemoryEffectState.Pending, workspace.Submissions[^1].State);
        store.ReleaseAll();
        await successor.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task Unknown_query_without_exact_operation_identity_stays_locked_without_replay() {
        var store = new MemoryScenarioStore(MemoryScenario.UnknownQuery);
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        await workspace.RunQueryAsync();
        var receipt = Assert.Single(workspace.Submissions);
        await workspace.ReviewAsync(receipt);
        await workspace.RefreshAsync();
        await workspace.RunQueryAsync();
        Assert.Equal(MemoryEffectState.Unknown, receipt.State);
        Assert.Equal(1, store.QueryCount);
        Assert.DoesNotContain("Synthetic lost", receipt.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Disposed_view_does_not_cancel_accepted_write_or_redirect_into_another_store() {
        var store = new MemoryScenarioStore();
        var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        store.HoldNext(MemoryScenarioLane.Save);
        workspace.Editor.DisplayName = "old accepted write";
        var save = workspace.SaveProviderAsync();
        workspace.Dispose();
        var nextStore = new MemoryScenarioStore();
        using var next = new MemoryProvidersPageController(nextStore);
        await next.RefreshAsync();
        store.ReleaseAll();
        await save.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("old accepted write", store.Profiles[MemoryScenarioStore.ProviderA].DisplayName);
        Assert.Equal("Business memory", next.Editor.DisplayName);
        Assert.Equal(0, nextStore.WriteCount);
    }

    [Fact]
    public async Task Changed_database_origin_refuses_dispatch_and_old_read_publication() {
        var store = new MemoryScenarioStore();
        var owner = new DelayedReads(store);
        using var workspace = new MemoryProvidersPageController(owner);
        await workspace.RefreshAsync();
        var held = owner.Hold();
        var read = workspace.RefreshAsync();
        store.IsCurrent = false;
        held.SetResult(await store.GetSnapshotAsync());
        await read.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(workspace.Snapshot);
        await workspace.SaveProviderAsync();
        Assert.Equal(0, store.WriteCount);
    }

    [Fact]
    public async Task Page_refresh_never_replays_query_or_status_and_accepted_handle_survives_failed_readback() {
        var store = new MemoryScenarioStore(MemoryScenario.AcceptedQuery);
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        workspace.QueryEditor.UseAsyncQuery = true;
        store.FailNextRead = true;
        await workspace.RunQueryAsync();
        var accepted = workspace.QueryResult!.AcceptedOperation!;
        Assert.Equal(workspace.Submissions[0].OperationId, accepted.OperationId);
        Assert.Equal(MemoryEffectState.ReadbackWarning, workspace.Submissions[0].State);
        await workspace.RefreshAsync();
        Assert.Equal(1, store.QueryCount);
        Assert.Equal(0, store.StatusCount);
        Assert.Equal(accepted.OperationId, Assert.Single(workspace.Snapshot!.Operations).OperationId);
    }

    [Fact]
    public async Task Empty_refresh_does_not_create_demos_and_explicit_creation_is_observed() {
        var store = new MemoryScenarioStore(MemoryScenario.Empty);
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        Assert.Empty(store.Profiles);
        await workspace.AddDemoProvidersAsync();
        Assert.Equal(2, store.WriteCount);
        await workspace.AddDemoProvidersAsync();
        Assert.Equal(2, store.WriteCount);
        Assert.Null(workspace.SelectedProvider);
        await workspace.SelectProviderAsync(MemoryDemoProviderIds.Business);
        Assert.NotNull(workspace.SelectedProvider);
    }

    [Fact]
    public async Task Partial_reads_and_bounded_history_are_explicit() {
        var store = new MemoryScenarioStore(MemoryScenario.Partial);
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        Assert.Contains(MemoryReadRegion.Feedback, workspace.Snapshot!.FailedRegions);
        Assert.Contains("stale", workspace.ErrorMessage!, StringComparison.Ordinal);
        for (var i = 0; i < 35; i++) {
            await workspace.SaveProviderAsync();
        }
        Assert.Equal(32, workspace.Submissions.Count);
    }

    [Fact]
    public async Task Unsupported_actions_remain_refused_without_changing_drafts() {
        var store = new MemoryScenarioStore();
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        workspace.FeedbackEditor.Comment = "kept";
        workspace.IngestionEditor.ContentText = "kept";
        await workspace.SubmitFeedbackAsync();
        await workspace.EnqueueManualIngestionAsync();
        Assert.All(workspace.Submissions, s => Assert.Equal(MemoryEffectState.Refused, s.State));
        Assert.Equal("kept", workspace.FeedbackEditor.Comment);
        Assert.Equal("kept", workspace.IngestionEditor.ContentText);
        Assert.Equal(0, store.WriteCount);
    }

    [Fact]
    public async Task Same_ID_profile_replacement_fences_old_query_while_retaining_raw_input() {
        var store = new MemoryScenarioStore();
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        store.HoldNext(MemoryScenarioLane.Query);
        var query = workspace.RunQueryAsync();
        var replacement = store.Profiles[MemoryScenarioStore.ProviderA].Capture();
        replacement.DisplayName = "Replaced profile";
        await store.SaveProviderAsync(replacement);
        workspace.Editor.DisplayName = "current raw edit";
        await workspace.RefreshAsync();
        store.ReleaseAll();
        await query.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Null(workspace.QueryResult);
        Assert.NotNull(workspace.Submissions[0].QueryResult);
        Assert.Equal("current raw edit", workspace.Editor.DisplayName);
        Assert.Equal("Replaced profile", workspace.SelectedProvider!.DisplayName);
    }

    [Fact]
    public async Task Incomplete_numeric_text_and_validation_survive_refresh_without_blur() {
        var store = new MemoryScenarioStore();
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.SelectProviderAsync("provider.http");
        using var context = Context();
        var cut = context.Render<MemoryWorkspaceSurface>(p => p.Add(c => c.Controller, workspace));
        await cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-editor-http-timeout']").Input("-"));
        await cut.InvokeAsync(workspace.RefreshAsync);
        Assert.Equal("-", workspace.Editor.Http.TimeoutText);
        Assert.Throws<InvalidOperationException>(() => workspace.Editor.Http.TimeoutMilliseconds);
        Assert.Equal("-", cut.Find("[data-testid='memory-ui-editor-http-timeout']").GetAttribute("value"));
        store.FailNextSave = true;
        await cut.InvokeAsync(workspace.SaveProviderAsync);
        await cut.InvokeAsync(workspace.RefreshAsync);
        Assert.Contains("validation refusal", workspace.ErrorMessage!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("http://remote.example/ui")]
    [InlineData("https://name:private@remote.example/ui")]
    [InlineData("https://remote.example/ui?token=private")]
    [InlineData("https://remote.example/ui#private")]
    public void Unsafe_surface_urls_are_rejected_and_never_projected(string url) {
        Assert.False(MemoryProviderUiUrlPolicy.TryNormalize(url, out var projected));
        Assert.Empty(projected);
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        return context;
    }

    private sealed class DelayedReads(MemoryScenarioStore store) : IMemoryProviderManagementUiService {
        private TaskCompletionSource<MemoryProviderManagementSnapshot>? next;
        public bool IsCurrent => store.IsCurrent;
        public TaskCompletionSource<MemoryProviderManagementSnapshot> Hold() => next = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<MemoryProviderManagementSnapshot> GetSnapshotAsync(string? id = null, CancellationToken cancellationToken = default) {
            var held = next;
            next = null;
            return held?.Task ?? store.GetSnapshotAsync(id, cancellationToken);
        }
        public Task<MemoryProviderProfile> SaveProviderAsync(MemoryProviderProfileEditorModel editor, CancellationToken cancellationToken = default) => store.SaveProviderAsync(editor, cancellationToken);
        public Task<IReadOnlyList<MemoryProviderProfile>> CreateDemoProvidersAsync(CancellationToken cancellationToken = default) => store.CreateDemoProvidersAsync(cancellationToken);
        public Task<MemoryProviderQueryUiResult> RunQueryAsync(string? id, MemoryQueryEditorModel editor, CancellationToken cancellationToken = default) => store.RunQueryAsync(id, editor, cancellationToken);
        public Task<MemoryProviderOperationUiResult> RefreshOperationAsync(string id, CancellationToken cancellationToken = default) => store.RefreshOperationAsync(id, cancellationToken);
        public Task<MemoryProviderOperationUiResult> CancelOperationAsync(string id, CancellationToken cancellationToken = default) => store.CancelOperationAsync(id, cancellationToken);
        public Task<MemoryProviderFeedbackUiResult> SubmitFeedbackAsync(string? id, MemoryFeedbackEditorModel editor, CancellationToken cancellationToken = default) => store.SubmitFeedbackAsync(id, editor, cancellationToken);
        public Task<MemoryProviderManualIngestionUiResult> EnqueueManualIngestionAsync(string? id, MemoryManualIngestionEditorModel editor, CancellationToken cancellationToken = default) => store.EnqueueManualIngestionAsync(id, editor, cancellationToken);
        public Task<MemoryProviderEventAcknowledgeUiResult> AcknowledgeEventAsync(string? id, string eventId, bool accepted, CancellationToken cancellationToken = default) => store.AcknowledgeEventAsync(id, eventId, accepted, cancellationToken);
    }
}
