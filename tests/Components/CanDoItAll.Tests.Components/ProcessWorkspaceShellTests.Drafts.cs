using Bunit;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Tests.Components.Processes;

public sealed partial class ProcessWorkspaceShellTests {
    public enum AuthoringFamily { Definition, Role, Step, Canvas, Template }

    public static IEnumerable<object[]> AuthoringReadOrders => Enum.GetValues<AuthoringFamily>()
        .SelectMany(family => new[] { new object[] { family, false }, [family, true] });

    [Theory]
    [MemberData(nameof(AuthoringReadOrders))]
    public async Task Authoring_all_families_keep_receipt_and_gate_across_read(AuthoringFamily family, bool readCompletesFirst) {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        cut.WaitForElement("[data-testid='processes-definition-save']");
        var session = (IProcessWorkspaceSession)cut.Instance;
        await OpenAuthoringFamilyAsync(cut, family);
        client.AuthoringCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = cut.InvokeAsync(() => SendAuthoringAsync(session, family));
        cut.WaitForAssertion(() => Assert.Equal(1, client.AuthoringCommandCount));
        client.DeferShellRequests = true;
        var reads = client.Requests.Count;
        var reading = cut.InvokeAsync(session.RefreshAsync);
        cut.WaitForAssertion(() => Assert.Equal(reads + 1, client.Requests.Count));
        if (readCompletesFirst) {
            client.CompleteShellRequest(0);
            await reading;
        }
        Assert.True(session.IsBusy);
        await cut.InvokeAsync(() => session.ExecuteDefinitionEditorCommandAsync(ProcessDefinitionEditorCommandKind.Archive));
        Assert.Equal(1, client.AuthoringCommandCount);
        client.AuthoringCompletion.SetResult();
        await running;
        var receipt = AuthoringReceipt(session, family);
        Assert.NotNull(receipt);
        if (!readCompletesFirst) {
            client.CompleteShellRequest(0);
            await reading;
        }
        Assert.Equal(receipt, AuthoringReceipt(session, family));
        Assert.False(session.IsBusy);
        Assert.Equal(1, client.AuthoringCommandCount);
        client.DeferShellRequests = false;
        await cut.InvokeAsync(session.RefreshAsync);
        Assert.Equal(receipt, AuthoringReceipt(session, family));
        Assert.Equal(1, client.AuthoringCommandCount);
    }

    [Theory]
    [InlineData(AuthoringFamily.Definition)]
    [InlineData(AuthoringFamily.Role)]
    [InlineData(AuthoringFamily.Step)]
    [InlineData(AuthoringFamily.Canvas)]
    [InlineData(AuthoringFamily.Template)]
    public async Task Authoring_unknown_result_blocks_replay_and_redacts_error_after_refresh(AuthoringFamily family) {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        cut.WaitForElement("[data-testid='processes-definition-save']");
        var session = (IProcessWorkspaceSession)cut.Instance;
        await OpenAuthoringFamilyAsync(cut, family);
        client.AuthoringCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = cut.InvokeAsync(() => SendAuthoringAsync(session, family));
        cut.WaitForAssertion(() => Assert.Equal(1, client.AuthoringCommandCount));
        await cut.InvokeAsync(session.RefreshAsync);
        client.AuthoringCompletion.SetException(new TimeoutException("Private native connection detail"));
        await running;
        Assert.Contains("outcome is unknown", session.ErrorMessage, StringComparison.Ordinal);
        await cut.InvokeAsync(session.RefreshAsync);
        await cut.InvokeAsync(() => SendAuthoringAsync(session, family));
        Assert.Equal(1, client.AuthoringCommandCount);
        Assert.True(session.IsBusy);
        Assert.Contains("outcome is unknown", session.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("Private native connection detail", cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(AuthoringFamily.Definition)]
    [InlineData(AuthoringFamily.Role)]
    [InlineData(AuthoringFamily.Step)]
    [InlineData(AuthoringFamily.Canvas)]
    [InlineData(AuthoringFamily.Template)]
    public async Task Authoring_known_refusal_releases_matching_submission_for_explicit_correction(AuthoringFamily family) {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        cut.WaitForElement("[data-testid='processes-definition-save']");
        var session = (IProcessWorkspaceSession)cut.Instance;
        await OpenAuthoringFamilyAsync(cut, family);
        client.RejectAuthoringCommands = true;
        client.AuthoringCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = cut.InvokeAsync(() => SendAuthoringAsync(session, family));
        cut.WaitForAssertion(() => Assert.Equal(1, client.AuthoringCommandCount));
        await cut.InvokeAsync(session.RefreshAsync);
        client.AuthoringCompletion.SetResult();
        await running;
        Assert.NotNull(AuthoringReceipt(session, family));
        Assert.False(session.IsBusy);
        Assert.Null(session.ErrorMessage);
        client.RejectAuthoringCommands = false;
        await cut.InvokeAsync(() => SendAuthoringAsync(session, family));
        Assert.Equal(2, client.AuthoringCommandCount);
        Assert.False(session.IsBusy);
    }

    [Theory]
    [MemberData(nameof(AuthoringReadOrders))]
    public async Task Authoring_late_success_error_and_finally_cannot_change_same_id_successor(AuthoringFamily family, bool fail) {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        cut.WaitForElement("[data-testid='processes-definition-save']");
        var session = (IProcessWorkspaceSession)cut.Instance;
        await OpenAuthoringFamilyAsync(cut, family);
        var key = session.DefinitionCatalog.SelectedEditor!.DefinitionKey;
        var original = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.AuthoringCompletion = original;
        var running = cut.InvokeAsync(() => SendAuthoringAsync(session, family));
        cut.WaitForAssertion(() => Assert.Equal(1, client.AuthoringCommandCount));
        await cut.InvokeAsync(() => session.SelectDefinitionAsync(new("architecture-decision-governance")));
        await cut.InvokeAsync(() => session.SelectDefinitionAsync(key));
        var successor = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        client.AuthoringCompletion = successor;
        var next = cut.InvokeAsync(() => SendAuthoringAsync(session, family));
        cut.WaitForAssertion(() => Assert.Equal(2, client.AuthoringCommandCount));
        if (fail) {
            original.SetException(new InvalidOperationException("Private retired owner"));
        } else {
            original.SetResult();
        }
        await running;
        Assert.True(session.IsBusy);
        Assert.Null(AuthoringReceipt(session, family));
        Assert.Null(session.ErrorMessage);
        successor.SetResult();
        await next;
        Assert.False(session.IsBusy);
        Assert.NotNull(AuthoringReceipt(session, family));
        Assert.DoesNotContain("Private retired owner", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Authoring_canvas_coalesces_final_positions_per_node_without_replaying_other_intents() {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        cut.WaitForElement("[data-testid='processes-definition-save']");
        var session = (IProcessWorkspaceSession)cut.Instance;
        await OpenAuthoringFamilyAsync(cut, AuthoringFamily.Canvas);
        var canvas = session.DefinitionCatalog.SelectedEditor!.Canvas!;
        var key = canvas.Nodes[0].NodeKey;
        ProcessDefinitionCanvasCommand Move(double x) => new(session.ResolveScope(), canvas.DefinitionKey,
            ProcessDefinitionCanvasCommandKind.MoveNodes, canvas.VersionToken, null, key, null,
            ProcessDefinitionCanvasRecompositionMode.PreserveProjection, [new(key, x, 30)]);
        client.AuthoringCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = cut.InvokeAsync(() => session.ExecuteDefinitionCanvasCommandAsync(Move(10)));
        cut.WaitForAssertion(() => Assert.Equal(1, client.AuthoringCommandCount));
        await cut.InvokeAsync(() => session.ExecuteDefinitionCanvasCommandAsync(Move(20)));
        await cut.InvokeAsync(session.RefreshAsync);
        await cut.InvokeAsync(() => session.ExecuteDefinitionCanvasCommandAsync(Move(40)));
        Assert.Single(client.CanvasCommands);
        client.AuthoringCompletion.SetResult();
        await running;
        Assert.Equal(2, client.CanvasCommands.Count);
        Assert.Equal(40, Assert.Single(client.CanvasCommands[1].NodePositions!).X);
        Assert.Equal(new("movenodes:test"), client.CanvasCommands[1].ExpectedVersionToken);
        Assert.Equal(canvas.DefinitionKey, client.CanvasCommands[1].DefinitionKey);
        Assert.False(session.IsBusy);
    }

    private static Task OpenAuthoringFamilyAsync(IRenderedComponent<ProcessWorkspaceShell> cut, AuthoringFamily family)
        => cut.InvokeAsync(() => ((IProcessWorkspaceSession)cut.Instance).HandleDetailTabChangedAsync((int)(family switch {
            AuthoringFamily.Role => ProcessWorkspaceDetailTabKey.Roles,
            AuthoringFamily.Step or AuthoringFamily.Canvas => ProcessWorkspaceDetailTabKey.Steps,
            AuthoringFamily.Template => ProcessWorkspaceDetailTabKey.Exchange,
            _ => ProcessWorkspaceDetailTabKey.Definition
        })));

    private static Task SendAuthoringAsync(IProcessWorkspaceSession session, AuthoringFamily family) {
        var editor = session.DefinitionCatalog.SelectedEditor!;
        return family switch {
            AuthoringFamily.Definition => session.ExecuteDefinitionEditorCommandAsync(ProcessDefinitionEditorCommandKind.SaveDraft),
            AuthoringFamily.Role => session.RoleEditorState.ExecuteAsync(ProcessDefinitionRoleCommandKind.SaveRole),
            AuthoringFamily.Step => session.StepEditorState.ExecuteAsync(ProcessDefinitionStepCommandKind.SaveStep),
            AuthoringFamily.Canvas => session.ExecuteDefinitionCanvasCommandAsync(new(session.ResolveScope(), editor.DefinitionKey,
                ProcessDefinitionCanvasCommandKind.Recompose, editor.Canvas!.VersionToken, null, null, null,
                ProcessDefinitionCanvasRecompositionMode.BalancedFlow)),
            AuthoringFamily.Template => session.ExecuteTemplateImportCommandAsync(new(session.ResolveScope(), editor.DefinitionKey,
                ProcessTemplateImportCommandKind.ImportProcess, editor.TemplateCatalog!.SelectedItem!.Key,
                editor.TemplateCatalog.VersionToken, editor.TemplateCatalog.Query, session.TemplateBrowserState.SelectedTargetStepKey)),
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
    }

    private static Guid? AuthoringReceipt(IProcessWorkspaceSession session, AuthoringFamily family) {
        var editor = session.DefinitionCatalog.SelectedEditor!;
        return family switch {
            AuthoringFamily.Definition => editor.LastCommandReceipt?.ReceiptId,
            AuthoringFamily.Role => editor.RoleEditor?.LastCommandReceipt?.ReceiptId,
            AuthoringFamily.Step => editor.StepEditor?.LastCommandReceipt?.ReceiptId,
            AuthoringFamily.Canvas => editor.Canvas?.LastCommandReceipt?.ReceiptId,
            AuthoringFamily.Template => editor.TemplateCatalog?.LastImportReceipt?.ReceiptId,
            _ => throw new ArgumentOutOfRangeException(nameof(family))
        };
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Authoring_definition_receipt_and_submission_survive_same_opening_read(bool readCompletesFirst) {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        cut.WaitForElement("[data-testid='processes-definition-save']");
        var session = (IProcessWorkspaceSession)cut.Instance;
        await cut.InvokeAsync(() => cut.Find("[data-testid='processes-definition-editor-name']")
            .InputAsync(new ChangeEventArgs { Value = "Submitted definition" }));
        client.DeferEditorCommands = true;
        var saving = cut.InvokeAsync(() => session.ExecuteDefinitionEditorCommandAsync(ProcessDefinitionEditorCommandKind.SaveDraft));
        cut.WaitForAssertion(() => Assert.Equal(1, client.EditorCommandCount));
        await cut.InvokeAsync(() => cut.Find("[data-testid='processes-definition-editor-owner']")
            .InputAsync(new ChangeEventArgs { Value = "Later owner" }));
        client.DeferShellRequests = true;
        var refreshing = cut.InvokeAsync(session.RefreshAsync);
        cut.WaitForAssertion(() => Assert.Equal(2, client.Requests.Count));
        if (readCompletesFirst) {
            client.CompleteShellRequest(0);
            await refreshing;
        }
        client.CompleteEditorCommand(0);
        await saving;
        if (!readCompletesFirst) {
            client.CompleteShellRequest(0);
            await refreshing;
        }
        Assert.Equal("Draft saved.", session.EditorCommandNotice);
        Assert.Equal("savedraft:test", session.DefinitionDraft.VersionToken?.Value);
        Assert.Equal("Submitted definition", cut.Find("[data-testid='processes-definition-editor-name']").GetAttribute("value"));
        Assert.Equal("Later owner", cut.Find("[data-testid='processes-definition-editor-owner']").GetAttribute("value"));
        Assert.Equal(1, client.EditorCommandCount);
        Assert.False(session.IsBusy);
    }

    [Fact]
    public async Task Authoring_refresh_cannot_release_slot_for_alternate_command() {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        cut.WaitForElement("[data-testid='processes-definition-save']");
        var session = (IProcessWorkspaceSession)cut.Instance;
        client.DeferEditorCommands = true;
        var saving = cut.InvokeAsync(() => session.ExecuteDefinitionEditorCommandAsync(ProcessDefinitionEditorCommandKind.SaveDraft));
        cut.WaitForAssertion(() => Assert.Equal(1, client.EditorCommandCount));
        await cut.InvokeAsync(session.RefreshAsync);
        var wasBusy = session.IsBusy;
        var publishing = cut.InvokeAsync(() => session.ExecuteDefinitionEditorCommandAsync(ProcessDefinitionEditorCommandKind.Publish));
        await cut.InvokeAsync(() => Task.CompletedTask);
        var commandCount = client.EditorCommandCount;
        for (var index = 0; index < commandCount; index++) {
            client.CompleteEditorCommand(index);
        }
        await Task.WhenAll(saving, publishing);
        Assert.True(wasBusy);
        Assert.Equal(1, commandCount);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Live_operator_receipt_survives_same_scope_read_but_never_updates_retired_scope(bool retarget, bool fail) {
        using var context = CreateContext(out var client);
        var cut = context.Render<LiveProcessesDashboard>();
        cut.WaitForElement("[data-testid='live-processes-activity-cards']");
        var session = (ILiveProcessesSession)cut.Instance;
        var action = session.Shell!.Runtime.Runs.SelectMany(run => run.OperatorActions).First(candidate => candidate.IsEnabled);
        client.OperatorCompletion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var running = cut.InvokeAsync(() => session.ExecuteOperatorActionAsync(action));
        cut.WaitForAssertion(() => Assert.NotNull(client.LastOperatorActionCommand));
        Assert.Equal(action.RunId, client.LastOperatorActionCommand!.RunId.Value);
        if (retarget) {
            cut.Render(parameters => parameters.Add(component => component.ProjectId, Guid.NewGuid()));
            cut.WaitForAssertion(() => Assert.Equal(ProcessWorkspaceScopeKind.Project, session.Shell!.Scope.Kind));
        } else {
            await cut.InvokeAsync(session.ForceRefreshAsync);
        }
        var reads = client.Requests.Count;
        if (fail) {
            client.OperatorCompletion.SetException(new InvalidOperationException("Private retired operator detail"));
        } else {
            client.OperatorCompletion.SetResult(new(new(action.RunId), new(action.StepInstanceId), action.Kind,
                ProcessRuntimeTransitionOutcome.Applied, ProcessRuntimeStatus.Active, []));
        }
        await running;
        Assert.False(session.IsOperatorActionBusy(action));
        Assert.DoesNotContain("Private retired operator detail", cut.Markup, StringComparison.Ordinal);
        if (retarget) {
            Assert.Null(session.OperatorActionMessage);
            Assert.Null(session.OperatorActionError);
            Assert.Equal(reads, client.Requests.Count);
        } else if (fail) {
            Assert.Contains("operator action failed", session.OperatorActionError, StringComparison.Ordinal);
        } else {
            Assert.Contains("accepted", session.OperatorActionMessage, StringComparison.Ordinal);
            Assert.Equal(reads + 1, client.Requests.Count);
        }
    }

    [Fact]
    public Task Workspace_transient_refresh_keeps_accepted_rows_but_current_denial_removes_them()
        => AssertReadFailureAsync<ProcessWorkspaceShell>("processes-refresh", component => ((IProcessWorkspaceSession)component).Shell);

    [Fact]
    public Task Live_transient_refresh_keeps_accepted_rows_but_current_denial_removes_them()
        => AssertReadFailureAsync<LiveProcessesDashboard>("live-processes-refresh-button", component => ((ILiveProcessesSession)component).Shell);

    private static async Task AssertReadFailureAsync<T>(string refreshId, Func<T, ProcessWorkspaceShellProjection?> projection) where T : IComponent {
        using var context = CreateContext(out var client);
        var cut = context.Render<T>();
        cut.WaitForAssertion(() => Assert.NotNull(projection(cut.Instance)));
        var accepted = projection(cut.Instance);
        client.ShellResultTransform = (_, _) => throw new TimeoutException("Private connection detail");
        await cut.Find($"[data-testid='{refreshId}']").ClickAsync(new MouseEventArgs());
        Assert.Same(accepted, projection(cut.Instance));
        Assert.Contains("last accepted projection", cut.Markup, StringComparison.Ordinal);
        Assert.DoesNotContain("Private connection detail", cut.Markup, StringComparison.Ordinal);
        client.ShellResultTransform = (_, _) => throw new UnauthorizedAccessException("Private admission detail");
        await cut.Find($"[data-testid='{refreshId}']").ClickAsync(new MouseEventArgs());
        Assert.Null(projection(cut.Instance));
        Assert.DoesNotContain("Blazor app delivery", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Definition_draft_survives_server_tab_unmount_and_same_definition_refresh() {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        cut.WaitForElement("[data-testid='processes-definition-editor-name']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='processes-definition-editor-name']")
            .InputAsync(new ChangeEventArgs { Value = "Unsaved definition sentinel" }));
        await cut.InvokeAsync(() => cut.Find("[data-testid='processes-definition-editor-owner']")
            .InputAsync(new ChangeEventArgs { Value = "Unsaved owner sentinel" }));

        await cut.InvokeAsync(() => cut.Find("[data-testid='processes-detail-tab-runs']").ClickAsync(new MouseEventArgs()));
        Assert.Empty(cut.FindAll("[data-testid='processes-definition-editor-name']"));
        await cut.InvokeAsync(() => cut.Find("[data-testid='processes-tab-definitions']").ClickAsync(new MouseEventArgs()));
        await cut.InvokeAsync(() => cut.Find("[data-testid='processes-refresh']").ClickAsync(new MouseEventArgs()));

        Assert.Equal("Unsaved definition sentinel", cut.Find("[data-testid='processes-definition-editor-name']").GetAttribute("value"));
        Assert.Equal("Unsaved owner sentinel", cut.Find("[data-testid='processes-definition-editor-owner']").GetAttribute("value"));
        Assert.Equal(0, client.EditorCommandCount);
        Assert.Null(client.LastRoleCommand);
        Assert.Null(client.LastStepCommand);
    }

    [Fact]
    public async Task Definition_save_preserves_edits_made_after_submission() {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessWorkspaceShell>();
        cut.WaitForElement("[data-testid='processes-definition-save']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='processes-definition-editor-name']")
            .InputAsync(new ChangeEventArgs { Value = "Submitted definition" }));
        client.DeferEditorCommands = true;
        var saving = cut.InvokeAsync(() => cut.Find("[data-testid='processes-definition-save']").ClickAsync(new MouseEventArgs()));
        cut.WaitForAssertion(() => Assert.Equal(1, client.EditorCommandCount));
        await cut.InvokeAsync(() => cut.Find("[data-testid='processes-definition-editor-owner']")
            .InputAsync(new ChangeEventArgs { Value = "Owner typed after submission" }));
        client.CompleteEditorCommand(0);
        await saving;

        Assert.Equal("Submitted definition", cut.Find("[data-testid='processes-definition-editor-name']").GetAttribute("value"));
        Assert.Equal("Owner typed after submission", cut.Find("[data-testid='processes-definition-editor-owner']").GetAttribute("value"));
        Assert.NotEqual("Owner typed after submission", client.LastEditorCommand?.Draft.Identity.OwnerName);
        Assert.Equal(1, client.EditorCommandCount);
    }
}
