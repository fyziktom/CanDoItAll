using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Processes;

public sealed class ProcessRunCancellationActionTests {
    [Fact]
    public async Task Retired_explicit_refresh_does_not_notify_the_parent_of_another_selection() {
        using var context = CreateContext(out var client);
        var runId = ProcessRunId.New();
        var observed = new List<ProcessRunId>();
        var cut = context.Render<ProcessRunCancellationAction>(parameters => parameters.Add(component => component.RunId, runId)
            .Add(component => component.StatusRefreshed, observed.Add));
        var pending = new TaskCompletionSource<ProcessCancellationClientView>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.Read = (id, _) => id == runId ? pending.Task : Task.FromResult(Ready(id));
        var refresh = cut.InvokeAsync(() => Refresh(cut).Click.InvokeAsync());
        await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.RunId, ProcessRunId.New())));
        pending.SetResult(View(runId, ProcessCancellationTracking.Completed, ProcessRuntimeStatus.Cancelled));
        await refresh;
        Assert.Empty(observed);
        Assert.Empty(client.Commands);
        Assert.DoesNotContain("Cleanup is complete", cut.Markup);
    }

    [Fact]
    public async Task Explicit_status_refresh_reconciles_the_parent_without_publishing_a_commit_or_running_a_command() {
        using var context = CreateContext(out var client);
        var runId = ProcessRunId.New();
        var observed = new List<ProcessRunId>();
        var cut = context.Render<ProcessRunCancellationAction>(parameters => parameters.Add(component => component.RunId, runId)
            .Add(component => component.CancellationCommitted, (ProcessRunId _) => throw new InvalidOperationException("Unexpected commit callback."))
            .Add(component => component.StatusRefreshed, observed.Add));
        Assert.Empty(observed);
        client.Read = (id, _) => Task.FromResult(View(id, ProcessCancellationTracking.Completed, ProcessRuntimeStatus.Cancelled));
        await cut.InvokeAsync(() => Refresh(cut).Click.InvokeAsync());
        Assert.Equal([runId], observed);
        Assert.Empty(client.Commands);
        Assert.Contains("Cleanup is complete", cut.Markup);
    }

    [Fact]
    public async Task Failed_parent_refresh_keeps_verified_status_and_retries_only_the_read_and_projection() {
        using var context = CreateContext(out var client);
        var runId = ProcessRunId.New();
        var notifications = 0;
        var cut = context.Render<ProcessRunCancellationAction>(parameters => parameters.Add(component => component.RunId, runId)
            .Add(component => component.StatusRefreshed, (ProcessRunId _) => {
                if (++notifications == 1) {
                    throw new InvalidOperationException("private projection error");
                }
            }));
        client.Read = (id, _) => Task.FromResult(View(id, ProcessCancellationTracking.Completed, ProcessRuntimeStatus.Cancelled));
        await cut.InvokeAsync(() => Refresh(cut).Click.InvokeAsync());
        Assert.Contains("Cleanup is complete", cut.Markup);
        Assert.Contains("Projection refresh failed", cut.Markup);
        Assert.DoesNotContain("private", cut.Markup);
        await cut.InvokeAsync(() => Refresh(cut).Click.InvokeAsync());
        Assert.Equal(2, notifications);
        Assert.DoesNotContain("refresh failed", cut.Markup);
        Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task Duplicate_intent_dispatches_once_and_reports_pending_without_claiming_completed_cleanup() {
        using var context = CreateContext(out var client);
        var runId = ProcessRunId.New();
        var committed = new List<ProcessRunId>();
        var cut = Render(context, runId, committed.Add);
        var click = Cancel(cut).Click;
        var pending = cut.InvokeAsync(() => click.InvokeAsync());
        cut.WaitForAssertion(() => Assert.Single(client.Commands));
        await cut.InvokeAsync(() => click.InvokeAsync());
        Assert.Single(client.Commands);
        Assert.True(Cancel(cut).Disabled);
        client.Completion.SetResult(Accepted(runId));
        await pending;
        Assert.Equal([runId], committed);
        Assert.Contains("Cleanup is still pending", cut.Markup);
        Assert.DoesNotContain("Cleanup is complete", cut.Markup);
        await cut.InvokeAsync(() => click.InvokeAsync());
        Assert.Single(client.Commands);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retiring_the_view_does_not_cancel_the_command_or_publish_a_late_result(bool dispose) {
        using var context = CreateContext(out var client);
        var original = ProcessRunId.New();
        var committed = new List<ProcessRunId>();
        var cut = Render(context, original, committed.Add);
        var pending = cut.InvokeAsync(() => Cancel(cut).Click.InvokeAsync());
        cut.WaitForAssertion(() => Assert.Single(client.Commands));
        if (dispose) {
            await context.DisposeRenderedComponentsAsync();
        } else {
            await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.RunId, ProcessRunId.New())));
        }
        Assert.False(client.CommandToken.IsCancellationRequested);
        client.Completion.SetResult(Accepted(original));
        await pending;
        Assert.Empty(committed);
        if (!dispose) {
            Assert.DoesNotContain("Cleanup is still pending", cut.Markup);
            Assert.False(Cancel(cut).Disabled);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_reads_are_cancelled_and_a_late_status_never_replaces_the_new_selection(bool dispose) {
        using var context = CreateContext(out var client);
        var original = ProcessRunId.New();
        var waiting = new TaskCompletionSource<ProcessCancellationClientView>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken originalToken = default;
        client.Read = (runId, token) => {
            if (runId != original) {
                return Task.FromResult(Ready(runId));
            }
            originalToken = token;
            return waiting.Task;
        };
        var cut = Render(context, original, _ => { });
        if (dispose) {
            await context.DisposeRenderedComponentsAsync();
        } else {
            await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.RunId, ProcessRunId.New())));
        }
        Assert.True(originalToken.IsCancellationRequested);
        waiting.SetResult(View(original, ProcessCancellationTracking.Completed, ProcessRuntimeStatus.Cancelled));
        await cut.InvokeAsync(() => Task.CompletedTask);
        if (!dispose) {
            cut.WaitForAssertion(() => Assert.False(Cancel(cut).Disabled));
            Assert.DoesNotContain("Cleanup is complete", cut.Markup);
        }
    }

    [Fact]
    public async Task Projection_and_read_failures_preserve_known_admission_then_read_only_refresh_confirms_completion() {
        using var context = CreateContext(out var client);
        var runId = ProcessRunId.New();
        var cut = context.Render<ProcessRunCancellationAction>(parameters => parameters
            .Add(component => component.RunId, runId)
            .Add(component => component.Status, ProcessProjectedRunStatus.Active)
            .Add(component => component.CancellationCommitted, (ProcessRunId _) => Task.FromException(new InvalidOperationException("private projection detail"))));
        client.Completion.SetResult(Accepted(runId));
        await cut.InvokeAsync(() => Cancel(cut).Click.InvokeAsync());
        Assert.Contains("Cleanup is still pending", cut.Markup);
        Assert.Contains("Projection refresh failed", cut.Markup);
        client.Read = (_, _) => throw new IOException("private read detail");
        await cut.InvokeAsync(() => Refresh(cut).Click.InvokeAsync());
        Assert.Contains("Cleanup is still pending", cut.Markup);
        Assert.Contains("Status refresh failed", cut.Markup);
        Assert.DoesNotContain("private", cut.Markup);
        client.Read = (id, _) => Task.FromResult(View(id, ProcessCancellationTracking.Completed, ProcessRuntimeStatus.Cancelled));
        await cut.InvokeAsync(() => Refresh(cut).Click.InvokeAsync());
        Assert.Contains("Run cancelled. Cleanup is complete", cut.Markup);
        Assert.DoesNotContain("refresh failed", cut.Markup);
        Assert.Single(client.Commands);
        Assert.Empty(cut.FindAll("[data-testid=process-run-cancel]"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Unknown_outcome_is_not_cleared_or_replayed_by_an_empty_observation(bool typedOutcome) {
        using var context = CreateContext(out var client);
        var runId = ProcessRunId.New();
        var cut = Render(context, runId, _ => throw new InvalidOperationException("Unexpected callback."));
        if (typedOutcome) {
            client.Completion.SetResult(new ProcessCancellationClientResult.Root(new(runId, ProcessRootCancellationAdmissionStatus.Unconfirmed)));
        } else {
            client.Completion.SetException(new IOException("private acknowledgement detail"));
        }
        await cut.InvokeAsync(() => Cancel(cut).Click.InvokeAsync());
        Assert.Contains("outcome could not be confirmed", cut.Markup);
        await cut.InvokeAsync(() => Refresh(cut).Click.InvokeAsync());
        Assert.Contains("original request outcome remains unconfirmed", cut.Markup);
        Assert.True(Cancel(cut).Disabled);
        await cut.InvokeAsync(() => Cancel(cut).Click.InvokeAsync());
        Assert.Single(client.Commands);
        Assert.DoesNotContain("private", cut.Markup);
        client.Read = (id, _) => Task.FromResult(View(id, ProcessCancellationTracking.Pending, ProcessRuntimeStatus.CancelRequested));
        await cut.InvokeAsync(() => Refresh(cut).Click.InvokeAsync());
        Assert.Contains("Cleanup is still pending", cut.Markup);
        Assert.Single(client.Commands);
    }

    [Theory]
    [InlineData(ProcessCancellationTracking.Pending, ProcessRuntimeStatus.CancelRequested, "Cleanup is still pending")]
    [InlineData(ProcessCancellationTracking.Completed, ProcessRuntimeStatus.Cancelled, "Cleanup is complete")]
    [InlineData(ProcessCancellationTracking.LegacyUntracked, ProcessRuntimeStatus.Cancelled, "Cleanup has not been verified")]
    [InlineData(ProcessCancellationTracking.ReconciliationRequired, ProcessRuntimeStatus.Cancelled, "requires reconciliation")]
    [InlineData(ProcessCancellationTracking.ChildRun, ProcessRuntimeStatus.Cancelled, "Root cleanup is tracked separately")]
    [InlineData(ProcessCancellationTracking.NotRequested, ProcessRuntimeStatus.Completed, "already finished")]
    [InlineData(ProcessCancellationTracking.NotRequested, ProcessRuntimeStatus.Failed, "already finished")]
    public void Initial_observation_distinguishes_cleanup_from_runtime_status(ProcessCancellationTracking tracking, ProcessRuntimeStatus status, string expected) {
        using var context = CreateContext(out var client);
        client.Read = (id, _) => Task.FromResult(View(id, tracking, status));
        var cut = Render(context, ProcessRunId.New(), _ => { });
        Assert.Contains(expected, cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid=process-run-cancel]"));
        Assert.False(Refresh(cut).Disabled);
        Assert.Empty(client.Commands);
    }

    [Fact]
    public void A_cancelled_projection_does_not_hide_pending_recovery_or_read_only_refresh() {
        using var context = CreateContext(out var client);
        client.Read = (id, _) => Task.FromResult(View(id, ProcessCancellationTracking.Pending, ProcessRuntimeStatus.Cancelled));
        var cut = context.Render<ProcessRunCancellationAction>(parameters => parameters
            .Add(component => component.RunId, ProcessRunId.New()).Add(component => component.Status, ProcessProjectedRunStatus.Cancelled));
        Assert.Contains("Cleanup is still pending", cut.Markup);
        Assert.DoesNotContain("Cleanup is complete", cut.Markup);
        Assert.False(Refresh(cut).Disabled);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Permission_and_root_worker_availability_control_the_action_without_hiding_refresh(bool permission, bool enabled) {
        using var context = CreateContext(out var client);
        client.Read = (id, _) => Task.FromResult(Ready(id) with { CanRequest = permission, RootRecoveryAvailable = enabled });
        var cut = Render(context, ProcessRunId.New(), _ => { });
        Assert.Equal(!(permission && enabled), Cancel(cut).Disabled);
        Assert.False(Refresh(cut).Disabled);
        Assert.Empty(client.Commands);
    }

    [Fact]
    public async Task A_receipt_for_another_run_is_unconfirmed_and_never_publishes_a_commit_callback() {
        using var context = CreateContext(out var client);
        var cut = Render(context, ProcessRunId.New(), _ => throw new InvalidOperationException("Unexpected callback."));
        client.Completion.SetResult(Accepted(ProcessRunId.New()));
        await cut.InvokeAsync(() => Cancel(cut).Click.InvokeAsync());
        Assert.Contains("outcome could not be confirmed", cut.Markup);
        Assert.True(Cancel(cut).Disabled);
    }

    [Fact]
    public async Task Revoked_read_permission_removes_the_action_and_exposes_no_new_status() {
        using var context = CreateContext(out var client);
        var cut = Render(context, ProcessRunId.New(), _ => { });
        client.Read = (_, _) => throw new ProcessCancellationAccessDeniedException();
        await cut.InvokeAsync(() => Refresh(cut).Click.InvokeAsync());
        Assert.Contains("Current Process read access is required", cut.Markup);
        Assert.Empty(cut.FindAll("[data-testid=process-run-cancel]"));
        Assert.False(Refresh(cut).Disabled);
    }

    [Fact]
    public async Task Selected_child_receipt_never_claims_root_cleanup_completion() {
        using var context = CreateContext(out var client);
        var runId = ProcessRunId.New();
        client.Read = (id, _) => Task.FromResult(View(id, ProcessCancellationTracking.ChildRun, ProcessRuntimeStatus.Active));
        var committed = new List<ProcessRunId>();
        var cut = Render(context, runId, committed.Add);
        client.Completion.SetResult(new ProcessCancellationClientResult.Child(new(runId, ProcessRuntimeOperatorActionKind.CancelRun,
            ProcessRuntimeTransitionOutcome.Applied, ProcessRuntimeStatus.Cancelled, [])));
        await cut.InvokeAsync(() => Cancel(cut).Click.InvokeAsync());
        Assert.Contains("Selected child stopped. Root cleanup is tracked separately", cut.Markup);
        Assert.DoesNotContain("Cleanup is complete", cut.Markup);
        Assert.Equal([runId], committed);
    }

    [Fact]
    public async Task A_known_refusal_stays_explicit_after_read_only_refresh_without_automatic_retry() {
        using var context = CreateContext(out var client);
        var runId = ProcessRunId.New();
        var cut = Render(context, runId, _ => throw new InvalidOperationException("Unexpected callback."));
        client.Completion.SetResult(new ProcessCancellationClientResult.Root(new(runId, ProcessRootCancellationAdmissionStatus.Contended)));
        await cut.InvokeAsync(() => Cancel(cut).Click.InvokeAsync());
        await cut.InvokeAsync(() => Refresh(cut).Click.InvokeAsync());
        Assert.Contains("Cancellation was not accepted. Reopen this run", cut.Markup);
        Assert.True(Cancel(cut).Disabled);
        Assert.Single(client.Commands);
    }

    private static Button Cancel(IRenderedComponent<ProcessRunCancellationAction> cut)
        => cut.FindComponents<Button>().Single(item => item.Instance.Text == "Cancel run").Instance;
    private static Button Refresh(IRenderedComponent<ProcessRunCancellationAction> cut)
        => cut.FindComponents<Button>().Single(item => item.Instance.Text == "Refresh status").Instance;
    private static IRenderedComponent<ProcessRunCancellationAction> Render(BunitContext context, ProcessRunId runId, Action<ProcessRunId> committed) =>
        context.Render<ProcessRunCancellationAction>(parameters => parameters.Add(component => component.RunId, runId)
            .Add(component => component.Status, ProcessProjectedRunStatus.Active).Add(component => component.CancellationCommitted, committed));
    private static ProcessCancellationClientResult Accepted(ProcessRunId runId)
        => new ProcessCancellationClientResult.Root(new(runId, ProcessRootCancellationAdmissionStatus.Accepted, RuntimeCommandId.New()));
    private static ProcessCancellationClientView Ready(ProcessRunId runId)
        => View(runId, ProcessCancellationTracking.NotRequested, ProcessRuntimeStatus.Active);
    private static ProcessCancellationClientView View(ProcessRunId runId, ProcessCancellationTracking tracking, ProcessRuntimeStatus status)
        => new(new(runId, tracking == ProcessCancellationTracking.ChildRun ? ProcessRunId.New() : runId, status,
            DateTimeOffset.UtcNow, tracking), true, true);
    private static BunitContext CreateContext(out CancellationClient client) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        client = new();
        context.Services.AddSingleton<IProcessCancellationClient>(client);
        return context;
    }

    private sealed class CancellationClient : IProcessCancellationClient {
        internal List<ProcessRunId> Commands { get; } = [];
        internal TaskCompletionSource<ProcessCancellationClientResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal CancellationToken CommandToken { get; private set; }
        internal Func<ProcessRunId, CancellationToken, Task<ProcessCancellationClientView>> Read { get; set; }
            = (id, _) => Task.FromResult(Ready(id));
        public Task<ProcessCancellationClientView> ReadAsync(ProcessRunId runId, CancellationToken cancellationToken) => Read(runId, cancellationToken);
        public Task<ProcessCancellationClientResult> RequestAsync(ProcessRunId runId, CancellationToken cancellationToken) {
            Commands.Add(runId);
            CommandToken = cancellationToken;
            return Completion.Task;
        }
    }
}
