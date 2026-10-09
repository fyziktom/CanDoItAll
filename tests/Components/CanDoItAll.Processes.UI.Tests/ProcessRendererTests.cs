using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.UI;
using CanDoItAll.Processes.UiSandbox;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProcessesUI;

public sealed class ProcessRendererTests {
    [Fact]
    public async Task Unsubmitted_template_search_and_target_survive_server_tab_unmount_and_preview_change() {
        using var context = Context();
        var session = new ProcessScenarioSession();
        var cut = context.Render<ProcessWorkspaceSurface>(p => p.Add(c => c.Session, session));
        await cut.Find("[data-testid='processes-detail-tab-exchange']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='processes-template-library-search']").InputAsync(new ChangeEventArgs { Value = "Unsubmitted search" });
        var target = session.TemplateBrowserState.SelectedTargetStepKey;
        await cut.Find("[data-testid='processes-detail-tab-runs']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='processes-detail-tab-exchange']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='processes-template-library-preview-tab-markdown']").ClickAsync(new MouseEventArgs());
        Assert.Equal("Unsubmitted search", cut.Find("[data-testid='processes-template-library-search']").GetAttribute("value"));
        Assert.Equal(target, session.TemplateBrowserState.SelectedTargetStepKey);
        Assert.Null(session.DefinitionCatalog.SelectedEditor!.TemplateCatalog!.LastImportReceipt);
    }

    [Theory]
    [InlineData(0, "processes-definition-editor-name")]
    [InlineData(1, "processes-definition-role-editor")]
    [InlineData(2, "processes-definition-step-editor")]
    [InlineData(3, "processes-runs-tab-shell")]
    [InlineData(4, "processes-process-graphs-tab")]
    [InlineData(5, "processes-analytics-tab")]
    [InlineData(6, "processes-template-library")]
    [InlineData(7, "processes-manager-chat-tab")]
    public async Task Every_workspace_tab_renders_its_actual_children(int tab, string expected) {
        using var context = Context();
        var session = new ProcessScenarioSession();
        await session.HandleDetailTabChangedAsync(tab);
        var cut = context.Render<ProcessWorkspaceSurface>(p => p.Add(c => c.Session, session));
        Assert.Single(cut.FindAll($"[data-testid='{expected}']"));
        Assert.DoesNotContain("Unable to", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Role_invalid_guid_and_allocation_survive_actual_server_tab_unmount() {
        using var context = Context();
        var session = new ProcessScenarioSession();
        var cut = context.Render<ProcessWorkspaceSurface>(p => p.Add(c => c.Session, session));
        await cut.Find("[data-testid='processes-detail-tab-roles']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='processes-role-solution-architect']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='processes-role-executor-kind']").ChangeAsync(new ChangeEventArgs { Value = "Workflow" });
        await cut.Find("[data-testid='processes-role-workflow-id']").ChangeAsync(new ChangeEventArgs { Value = "invalid-guid" });
        await cut.Find("[data-testid='processes-role-allocation']").InputAsync(new ChangeEventArgs { Value = "101" });
        await cut.Find("[data-testid='processes-detail-tab-runs']").ClickAsync(new MouseEventArgs());
        Assert.Empty(cut.FindAll("[data-testid='processes-role-workflow-id']"));
        await cut.Find("[data-testid='processes-detail-tab-roles']").ClickAsync(new MouseEventArgs());
        Assert.Equal("invalid-guid", cut.Find("[data-testid='processes-role-workflow-id']").GetAttribute("value"));
        Assert.Equal("101", cut.Find("[data-testid='processes-role-allocation']").GetAttribute("value"));
        Assert.True(cut.Find("[data-testid='processes-role-save']").HasAttribute("disabled"));
        Assert.True(session.RoleEditorState.IsDirty);
        Assert.Null(session.DefinitionCatalog.SelectedEditor!.RoleEditor!.LastCommandReceipt);
    }

    [Fact]
    public async Task Step_raw_input_survives_echo_conflicts_explicit_discard_and_tab_unmount() {
        using var context = Context();
        var session = new ProcessScenarioSession();
        var cut = context.Render<ProcessWorkspaceSurface>(p => p.Add(c => c.Session, session));
        await cut.Find("[data-testid='processes-detail-tab-steps']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='processes-step-target-lead-hours']").InputAsync(new ChangeEventArgs { Value = "-1" });
        await cut.Find("[data-testid='processes-detail-tab-runs']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='processes-detail-tab-steps']").ClickAsync(new MouseEventArgs());
        Assert.Equal("-1", cut.Find("[data-testid='processes-step-target-lead-hours']").GetAttribute("value"));
        var state = session.StepEditorState;
        var previous = state.SyncedVersionToken;
        state.StepEditor = state.StepEditor with { VersionToken = new("new-authoritative-version") };
        state.Observe();
        Assert.True(state.HasConflict);
        Assert.Equal(previous, state.SyncedVersionToken);
        state.Discard();
        Assert.False(state.IsDirty);
        Assert.False(state.HasInputErrors);
        Assert.Equal(new("new-authoritative-version"), state.SyncedVersionToken);
    }

    [Fact]
    public async Task Step_submission_is_a_snapshot_and_later_row_edit_merges_with_accepted_addition() {
        var state = new ProcessStepEditorState { StepEditor = ProcessScenarioData.CreateStepEditor(new("blazor-app-delivery")), Scope = ProcessWorkspaceShellScope.Global };
        state.Observe();
        ProcessDefinitionStepEditorCommand? sent = null;
        state.ExecuteCommand = EventCallback.Factory.Create<ProcessDefinitionStepEditorCommand>(new object(), command => sent = command);
        var row = state.BranchOutcomes[0];
        await state.ExecuteAsync(ProcessDefinitionStepCommandKind.AddBranchOutcome);
        state.UpdateBranchTitle(row.OutcomeKey, new() { Value = "Typed after send" });
        Assert.Equal(row.Title, sent!.Draft.BranchOutcomes[0].Title);
        var accepted = ProcessScenarioData.CreateStepEditor(sent.DefinitionKey, sent.Draft, new("accepted"), new([]), null, sent.CommandKind);
        state.Accept(accepted);
        Assert.Equal(2, state.BranchOutcomes.Count);
        Assert.Equal("Typed after send", state.BranchOutcomes.Single(candidate => candidate.OutcomeKey == row.OutcomeKey).Title);
        Assert.True(state.IsDirty);
        state.BranchOutcomes.Reverse();
        state.UpdateBranchLoopBudget(row.OutcomeKey, new() { Value = "9" });
        Assert.Equal(9, state.BranchOutcomes.Single(candidate => candidate.OutcomeKey == row.OutcomeKey).LoopBudget.MaximumRepeats);
        Assert.Equal(0, state.BranchOutcomes.Single(candidate => candidate.OutcomeKey != row.OutcomeKey).LoopBudget.MaximumRepeats);
    }

    [Fact]
    public async Task Two_canvases_have_distinct_floating_and_surface_identities() {
        using var context = Context();
        var canvas = ProcessScenarioData.CreateCanvas(new("blazor-app-delivery"));
        var a = context.Render<ProcessDefinitionCanvasPanel>(p => p.Add(c => c.Canvas, canvas).Add(c => c.Scope, ProcessWorkspaceShellScope.Global));
        var b = context.Render<ProcessDefinitionCanvasPanel>(p => p.Add(c => c.Canvas, canvas).Add(c => c.Scope, ProcessWorkspaceShellScope.Global));
        var first = a.FindComponents<CanvasFloatingWindow>().Select(c => c.Instance.WindowId).ToArray();
        var second = b.FindComponents<CanvasFloatingWindow>().Select(c => c.Instance.WindowId).ToArray();
        Assert.NotEmpty(first);
        Assert.Empty(first.Intersect(second));
        await a.InvokeAsync(a.Dispose);
        Assert.NotEmpty(b.FindComponents<CanvasFloatingWindow>());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Live_tabs_keep_real_runtime_content_and_scope(int tab) {
        using var context = Context();
        var session = new ProcessScenarioSession();
        await session.HandleTabChangedAsync(tab);
        var cut = context.Render<LiveProcessesSurface>(p => p.Add(c => c.Session, session));
        Assert.Contains("live-processes-dashboard", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(session.CurrentShell.Runtime.Runs);
        await session.InjectReadFailureAsync();
        cut.Render();
        Assert.Contains("last accepted projection", cut.Markup, StringComparison.Ordinal);
        Assert.NotEmpty(cut.FindAll("[data-testid='live-processes-refresh-button']"));
    }

    [Fact]
    public async Task Files_activate_real_markdown_and_return_to_current_listing() {
        using var context = Context();
        await using var files = new ProcessFileScenario(Guid.NewGuid());
        var cut = context.Render<ProcessRunFilesSurface>(p => p.Add(c => c.IsOpen, true).Add(c => c.View, files.View)
            .Add(c => c.InteractionComposition, context.Services.GetRequiredService<FileInteractionComponentComposition>())
            .Add(c => c.ActivateAsync, files.ActivateAsync).Add(c => c.RefreshAsync, files.RefreshAsync));
        cut.WaitForAssertion(() => Assert.Contains("evidence.md", cut.Markup, StringComparison.Ordinal));
        var browser = cut.FindComponent<CanDoItAll.FileTools.FileBrowser.Components.FileBrowser>();
        var snapshot = browser.Instance.Session!.Snapshot;
        var item = snapshot.Items.Single();
        await cut.InvokeAsync(() => files.ActivateAsync(new(item, CanDoItAll.FileTools.FileBrowser.Components.FileBrowserInvocationKind.Keyboard)));
        cut.Render(p => p.Add(c => c.View, files.View));
        cut.WaitForAssertion(() => Assert.Contains("Process evidence", cut.Find("[data-testid='interaction-markdown-view']").TextContent, StringComparison.Ordinal));
        await cut.Find("[data-testid='process-run-files-back']").ClickAsync(new MouseEventArgs());
        cut.Render(p => p.Add(c => c.View, files.View));
        cut.WaitForAssertion(() => Assert.Contains("evidence.md", cut.Markup, StringComparison.Ordinal));
    }

    private static BunitContext Context() {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        context.Services.AddSingleton(new FileInteractionComponentBuilder().AddBuiltIns().AddMarkdown().Build());
        return context;
    }
}
