using System.Reflection;
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
    public async Task Duplicate_intent_dispatches_once_and_keeps_the_committed_identity() {
        using var context = CreateContext(out var client);
        var runId = ProcessRunId.New();
        var committed = new List<ProcessRunId>();
        var cut = Render(context, runId, committed.Add);
        var click = cut.FindComponent<Button>().Instance.Click;
        var pending = cut.InvokeAsync(() => click.InvokeAsync());
        cut.WaitForAssertion(() => Assert.Single(client.Commands));
        await cut.InvokeAsync(() => click.InvokeAsync());
        Assert.Single(client.Commands);
        Assert.True(cut.FindComponent<Button>().Instance.Disabled);
        client.Completion.SetResult(Success(runId));
        await pending;
        Assert.Equal([runId], committed);
        Assert.Contains("Run cancelled.", cut.Markup);
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
        var pending = cut.InvokeAsync(() => cut.FindComponent<Button>().Instance.Click.InvokeAsync());
        cut.WaitForAssertion(() => Assert.Single(client.Commands));
        if (dispose) {
            await context.DisposeRenderedComponentsAsync();
        } else {
            await cut.InvokeAsync(() => cut.Render(parameters => parameters.Add(component => component.RunId, ProcessRunId.New())));
        }
        Assert.False(client.CancellationToken.IsCancellationRequested);
        client.Completion.SetResult(Success(original));
        await pending;
        Assert.Empty(committed);
        if (!dispose) {
            Assert.DoesNotContain("Run cancelled.", cut.Markup);
            Assert.False(cut.FindComponent<Button>().Instance.Disabled);
        }
    }

    [Fact]
    public async Task Refresh_failure_preserves_known_commit_and_prevents_replay() {
        using var context = CreateContext(out var client);
        var runId = ProcessRunId.New();
        var cut = context.Render<ProcessRunCancellationAction>(parameters => parameters
            .Add(component => component.RunId, runId)
            .Add(component => component.Status, ProcessProjectedRunStatus.Active)
            .Add(component => component.CancellationCommitted, (ProcessRunId _) => Task.FromException(new InvalidOperationException("private refresh detail"))));
        client.Completion.SetResult(Success(runId));
        await cut.InvokeAsync(() => cut.FindComponent<Button>().Instance.Click.InvokeAsync());
        Assert.Contains("Run cancelled.", cut.Markup);
        Assert.Contains("Refresh failed", cut.Markup);
        Assert.DoesNotContain("private refresh detail", cut.Markup);
        Assert.True(cut.FindComponent<Button>().Instance.Disabled);
    }

    [Fact]
    public async Task Unknown_command_outcome_is_explicit_and_cannot_be_blindly_replayed() {
        using var context = CreateContext(out var client);
        var cut = Render(context, ProcessRunId.New(), _ => throw new InvalidOperationException("Unexpected commit callback."));
        client.Completion.SetException(new InvalidOperationException("private command detail"));
        await cut.InvokeAsync(() => cut.FindComponent<Button>().Instance.Click.InvokeAsync());
        Assert.Contains("outcome could not be confirmed", cut.Markup);
        Assert.DoesNotContain("private command detail", cut.Markup);
        Assert.True(cut.FindComponent<Button>().Instance.Disabled);
    }

    [Theory]
    [InlineData(ProcessProjectedRunStatus.Completed)]
    [InlineData(ProcessProjectedRunStatus.Cancelled)]
    [InlineData(ProcessProjectedRunStatus.Failed)]
    [InlineData(ProcessProjectedRunStatus.Unknown)]
    public void Non_active_run_has_no_cancellation_action(ProcessProjectedRunStatus status) {
        using var context = CreateContext(out var client);
        var cut = context.Render<ProcessRunCancellationAction>(parameters => parameters
            .Add(component => component.RunId, ProcessRunId.New()).Add(component => component.Status, status));
        Assert.Empty(cut.FindComponents<Button>());
        Assert.Empty(client.Commands);
    }

    private static IRenderedComponent<ProcessRunCancellationAction> Render(BunitContext context, ProcessRunId runId, Action<ProcessRunId> committed) =>
        context.Render<ProcessRunCancellationAction>(parameters => parameters.Add(component => component.RunId, runId)
            .Add(component => component.Status, ProcessProjectedRunStatus.Active).Add(component => component.CancellationCommitted, committed));

    private static ProcessRuntimeRunCancellationResult Success(ProcessRunId runId) =>
        new(runId, ProcessRuntimeOperatorActionKind.CancelRun, ProcessRuntimeTransitionOutcome.Applied, ProcessRuntimeStatus.Cancelled, []);

    private static BunitContext CreateContext(out CancellationClient client) {
        var context = new BunitContext();
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        context.Services.AddLogging();
        context.Services.AddCanDoItAllBaseLib();
        var proxy = DispatchProxy.Create<IProcessWorkspaceProjectionClient, CancellationClient>();
        client = (CancellationClient)(object)proxy;
        context.Services.AddSingleton(proxy);
        return context;
    }

    public class CancellationClient : DispatchProxy {
        public List<ProcessRuntimeRunCancellationCommand> Commands { get; } = [];
        public TaskCompletionSource<ProcessRuntimeRunCancellationResult> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken CancellationToken { get; private set; }

        protected override object? Invoke(MethodInfo? method, object?[]? arguments) {
            if (method?.Name != nameof(IProcessWorkspaceProjectionClient.RequestRunCancellationAsync)) {
                throw new InvalidOperationException("Unexpected projection call: " + method?.Name);
            }
            Commands.Add((ProcessRuntimeRunCancellationCommand)arguments![0]!);
            CancellationToken = (CancellationToken)arguments[1]!;
            return Completion.Task;
        }
    }
}
