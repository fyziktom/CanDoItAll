using Bunit;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace CanDoItAll.Tests.Components.Processes;

public sealed partial class ProcessWorkspaceShellTests {
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
