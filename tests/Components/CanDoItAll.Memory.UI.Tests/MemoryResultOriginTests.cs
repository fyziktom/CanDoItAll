using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Memory.UI;
using CanDoItAll.Memory.UiSandbox;
using CanDoItAll.Modules.Memory.Pages;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.MemoryUi;

public sealed class MemoryResultOriginTests {
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task Replacement_or_removal_retires_visible_and_own_readback_results(bool held, bool remove) {
        var store = new MemoryScenarioStore();
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        workspace.ActiveTabIndex = 4;
        using var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddCanDoItAllBaseLib();
        var cut = context.Render<MemoryWorkspaceSurface>(p => p.Add(c => c.Controller, workspace));
        await cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-tab-query']").Click());
        if (held) {
            store.HoldNext(MemoryScenarioLane.Query);
        }
        var query = cut.InvokeAsync(workspace.RunQueryAsync);
        if (!held) {
            await query;
            Assert.Single(cut.FindAll("[data-testid='memory-ui-query'] [data-testid='memory-ui-query-result']"));
        }
        var original = store.Profiles[MemoryScenarioStore.ProviderA].Capture();
        var replacement = original.Capture();
        replacement.DisplayName = "Replacement revision";
        if (remove) {
            store.Remove(MemoryScenarioStore.ProviderA);
        } else {
            await store.SaveProviderAsync(replacement);
        }
        await cut.InvokeAsync(() => cut.Find("[data-testid='memory-ui-query-text']").Input("unfinished query"));
        workspace.FeedbackEditor.Comment = "unfinished feedback";
        if (held) {
            store.ReleaseAll();
            await query.WaitAsync(TimeSpan.FromSeconds(5));
        } else {
            await cut.InvokeAsync(workspace.RefreshAsync);
        }
        Assert.Null(workspace.QueryResult);
        Assert.Empty(cut.FindAll("[data-testid='memory-ui-query'] [data-testid='memory-ui-query-result']"));
        var receipt = Assert.Single(workspace.Submissions);
        Assert.Single(cut.FindAll("[data-testid='memory-ui-historical-query']"));
        Assert.NotNull(receipt.QueryResult!.ContextPack);
        Assert.Equal(receipt.OperationId, receipt.QueryResult.Operation!.OperationId);
        Assert.Equal("unfinished query", workspace.QueryEditor.Query);
        Assert.Equal("unfinished feedback", workspace.FeedbackEditor.Comment);
        Assert.Empty(workspace.FeedbackEditor.ContextPackId);
        if (remove) {
            await store.SaveProviderAsync(original);
            await cut.InvokeAsync(workspace.RefreshAsync);
            Assert.Null(workspace.QueryResult);
        }
        Assert.Equal(1, store.QueryCount);
        Assert.Equal(0, store.StatusCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Same_revision_and_failed_reads_retain_results_and_accepted_identity(bool accepted) {
        var store = new MemoryScenarioStore(accepted ? MemoryScenario.AcceptedQuery : MemoryScenario.Populated);
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        workspace.QueryEditor.UseAsyncQuery = accepted;
        await workspace.RunQueryAsync();
        var result = workspace.QueryResult;
        var revision = workspace.Snapshot!.SelectedRevision;
        await workspace.RefreshAsync();
        Assert.Same(result, workspace.QueryResult);
        store.FailNextRead = true;
        await workspace.RefreshAsync();
        Assert.Same(result, workspace.QueryResult);
        Assert.Equal(revision, workspace.Draft.QuerySubmission!.ProviderRevision);
        var replacement = store.Profiles[MemoryScenarioStore.ProviderA].Capture();
        replacement.DisplayName = "Replacement";
        await store.SaveProviderAsync(replacement);
        await workspace.RefreshAsync();
        Assert.Null(workspace.QueryResult);
        Assert.Same(result, workspace.Submissions[0].QueryResult);
        Assert.Equal(result!.AcceptedOperation, workspace.Submissions[0].QueryResult!.AcceptedOperation);
        Assert.Equal(1, store.QueryCount);
        Assert.Equal(0, store.StatusCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Manual_feedback_context_survives_replacement_including_edit_away_and_back(bool editBack) {
        var store = new MemoryScenarioStore();
        using var workspace = new MemoryProvidersPageController(store);
        await workspace.RefreshAsync();
        if (!editBack) {
            workspace.FeedbackEditor.ContextPackId = "manual incomplete id";
        }
        await workspace.RunQueryAsync();
        var context = workspace.FeedbackEditor.ContextPackId;
        if (editBack) {
            workspace.FeedbackEditor.ContextPackId = "edited away";
            workspace.FeedbackEditor.ContextPackId = context;
        }
        workspace.FeedbackEditor.Comment = "unfinished comment";
        var replacement = store.Profiles[MemoryScenarioStore.ProviderA].Capture();
        replacement.DisplayName = "Replacement";
        await store.SaveProviderAsync(replacement);
        await workspace.RefreshAsync();
        Assert.Null(workspace.QueryResult);
        Assert.Equal(context, workspace.FeedbackEditor.ContextPackId);
        Assert.Equal("unfinished comment", workspace.FeedbackEditor.Comment);
    }
}
