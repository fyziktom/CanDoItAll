using Bunit;
using Bunit.TestDoubles;
using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.Processes;

public sealed partial class ProcessWorkspaceShellTests {
    [Fact]
    public async Task Host_shutdown_cancels_a_pending_live_read_before_services_are_disposed() {
        var clock = new ManualTimeProvider(Now);
        using var context = CreateContext(out var client, timeProvider: clock);
        var lifetime = Assert.IsType<ProcessTestHostLifetime>(
            context.Services.GetRequiredService<Microsoft.Extensions.Hosting.IHostApplicationLifetime>());
        var cut = context.Render<LiveProcessesDashboard>();
        cut.WaitForElement("[data-testid='live-processes-tabs']");
        cut.WaitForAssertion(() => Assert.False(((ILiveProcessesSession)cut.Instance).IsLoading));
        var initial = client.Requests.Count;
        client.DeferShellRequests = true;
        clock.Advance(TimeSpan.FromSeconds(10));
        cut.WaitForAssertion(() => {
            Assert.Equal(initial + 1, client.Requests.Count);
            Assert.True(((ILiveProcessesSession)cut.Instance).IsLoading);
        });

        lifetime.StopApplication();

        Assert.True(client.LastShellCancellationToken.IsCancellationRequested);
        clock.Advance(TimeSpan.FromMinutes(1));
        await cut.InvokeAsync(() => { });
        Assert.Equal(initial + 1, client.Requests.Count);
    }

    private sealed class ProcessTestHostLifetime : Microsoft.Extensions.Hosting.IHostApplicationLifetime, IDisposable {
        private readonly CancellationTokenSource stopping = new();
        public CancellationToken ApplicationStarted => CancellationToken.None;
        public CancellationToken ApplicationStopping => stopping.Token;
        public CancellationToken ApplicationStopped => CancellationToken.None;
        public void StopApplication() => stopping.Cancel();
        public void Dispose() => stopping.Dispose();
    }

    [Fact]
    public async Task Live_refresh_updates_the_open_run_dialog_when_execution_completes() {
        var clock = new ManualTimeProvider(Now);
        using var context = CreateContext(out var client, timeProvider: clock);
        var completed = false;
        client.ShellResultTransform = (_, projection) => projection with {
            Runtime = projection.Runtime with {
                Runs = projection.Runtime.Runs.Select(run => run with {
                    Status = completed ? ProcessProjectedRunStatus.Completed : ProcessProjectedRunStatus.Active,
                    IsActive = !completed,
                    ExecutableStepCount = 2,
                    CompletedStepCount = completed ? 2 : 0,
                    TerminalStepCount = completed ? 2 : 0,
                    ProgressLabel = completed ? "2 of 2 executable steps complete" : "0 of 2 executable steps complete"
                }).ToArray()
            }
        };
        var cut = context.Render<LiveProcessesDashboard>();
        cut.WaitForElement("[data-testid='live-processes-run-open-details']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='live-processes-run-open-details']")
            .ClickAsync(new MouseEventArgs()));
        // Opening the dialog selects its run, so the selection's read finishes before the live tick.
        cut.WaitForAssertion(() => {
            Assert.False(((ILiveProcessesSession)cut.Instance).IsLoading);
            Assert.Equal(2, client.Requests.Count);
            Assert.Contains("0 of 2 executable steps complete",
                cut.Find("[data-testid='live-processes-run-detail-dialog']").TextContent);
        });

        completed = true;
        clock.Advance(TimeSpan.FromSeconds(10));

        cut.WaitForAssertion(() => {
            var dialog = cut.Find("[data-testid='live-processes-run-detail-dialog']").TextContent;
            Assert.Contains("2 of 2 executable steps complete", dialog);
            Assert.DoesNotContain("0 of 2 executable steps complete", dialog);
            Assert.True(client.LastRequest!.ForceRefresh);
        });
    }

    [Fact]
    public async Task Live_refresh_does_not_overlap_reads_or_continue_after_disposal() {
        var clock = new ManualTimeProvider(Now);
        using var context = CreateContext(out var client, timeProvider: clock);
        var cut = context.Render<LiveProcessesDashboard>();
        cut.WaitForElement("[data-testid='live-processes-tabs']");
        var initialRequests = client.Requests.Count;
        client.DeferShellRequests = true;

        clock.Advance(TimeSpan.FromSeconds(10));
        cut.WaitForAssertion(() => Assert.Equal(initialRequests + 1, client.Requests.Count));
        clock.Advance(TimeSpan.FromMinutes(1));
        await cut.InvokeAsync(() => { });
        Assert.Equal(initialRequests + 1, client.Requests.Count);

        await context.DisposeRenderedComponentsAsync();
        clock.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(initialRequests + 1, client.Requests.Count);
    }

    [Fact]
    public async Task Live_refresh_leaves_historical_windows_under_manual_control() {
        var clock = new ManualTimeProvider(Now);
        using var context = CreateContext(out var client, timeProvider: clock);
        var cut = context.Render<LiveProcessesDashboard>();
        cut.WaitForElement("[data-testid='live-processes-history-window']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='live-processes-history-window']")
            .ChangeAsync(new ChangeEventArgs { Value = nameof(ProcessRuntimeHistoryWindow.OneDay) }));
        var initialRequests = client.Requests.Count;

        clock.Advance(TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(() => { });

        Assert.Equal(initialRequests, client.Requests.Count);
    }

    [Fact]
    public async Task Live_refresh_discards_the_open_dialog_when_its_project_lifetime_changes() {
        var clock = new ManualTimeProvider(Now);
        using var context = CreateContext(out var client, timeProvider: clock);
        var projectId = Guid.NewGuid();
        var original = new ProcessProjectionProjectBinding(Guid.NewGuid(), projectId, Guid.NewGuid());
        var successor = new ProcessProjectionProjectBinding(original.DatabaseProfileId, projectId, Guid.NewGuid());
        var replaced = false;
        client.ShellResultTransform = (_, projection) => projection with {
            ProjectBinding = replaced ? successor : original
        };
        var cut = context.Render<LiveProcessesDashboard>(parameters => parameters.Add(component => component.ProjectId, projectId));
        cut.WaitForElement("[data-testid='live-processes-run-open-details']");
        await cut.InvokeAsync(() => cut.Find("[data-testid='live-processes-run-open-details']")
            .ClickAsync(new MouseEventArgs()));
        cut.WaitForElement("[data-testid='live-processes-run-detail-dialog']");
        cut.WaitForAssertion(() => {
            Assert.False(((ILiveProcessesSession)cut.Instance).IsLoading);
            Assert.Equal(2, client.Requests.Count);
        });
        Assert.Contains("dialog=run", CurrentLocation(context), StringComparison.Ordinal);

        replaced = true;
        clock.Advance(TimeSpan.FromSeconds(10));

        cut.WaitForAssertion(() => {
            Assert.Equal(3, client.Requests.Count);
            Assert.Empty(cut.FindAll("[data-testid='live-processes-run-detail-dialog']"));
        });
        // The record of the replaced lifetime leaves the address without a history entry of its own.
        Assert.DoesNotContain("dialog=", CurrentLocation(context), StringComparison.Ordinal);
        Assert.True(((BunitNavigationManager)context.Services.GetRequiredService<NavigationManager>()).History.First().Options.ReplaceHistoryEntry);
    }
}
