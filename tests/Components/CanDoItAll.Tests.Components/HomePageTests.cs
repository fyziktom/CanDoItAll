using System.Globalization;
using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Web.Dashboard;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using DashboardHome = CanDoItAll.Web.Components.Pages.Home;

namespace CanDoItAll.Tests.Components;

public sealed class HomePageTests
{
    private static readonly DateTimeOffset InitialUtc = new(
        2026,
        7,
        22,
        12,
        0,
        0,
        TimeSpan.Zero);

    private static readonly Guid ProjectId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WorkflowId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid WorkflowRunId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid ProcessRunId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Renders_exact_quick_actions_while_loading_without_fake_usage_totals()
    {
        var pendingLoad = new TaskCompletionSource<DashboardSnapshotData>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        await using var harness = CreateHarness(
            _ => pendingLoad.Task);

        var cut = harness.RenderHome();

        AssertQuickActions(cut);
        Assert.Single(cut.FindComponents<LoadingState>());
        Assert.Empty(cut.FindComponents<CompactStat>());
        Assert.DoesNotContain("Total tokens", cut.Markup, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("known cost", cut.Markup, StringComparison.OrdinalIgnoreCase);

        pendingLoad.TrySetResult(CreateEmptyData(observedTokens: 1_000));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, cut.FindComponents<CompactStat>().Count);
            Assert.Contains("No projects yet", cut.Markup, StringComparison.Ordinal);
            Assert.Contains("No recent workflow runs", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(
                "Auto refresh in 5 min",
                cut.Find("[data-testid='dashboard-refresh-countdown']").TextContent.Trim());
        });
    }

    [Fact]
    public async Task Renders_populated_snapshot_routes_activity_modes_and_projection_lag()
    {
        await using var harness = CreateHarness(
            _ => Task.FromResult(CreatePopulatedData()));

        var cut = harness.RenderHome();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(
                12_345L.ToString("N0", CultureInfo.CurrentCulture),
                FindStat(cut, "Total tokens").Value);
            Assert.Equal("$0.1235", FindStat(cut, "Known cost (USD)").Value);
        });

        var costStat = FindStat(cut, "Known cost (USD)");
        Assert.Contains("excludes 2", costStat.TooltipText, StringComparison.Ordinal);

        var project = cut.Find($"[data-testid='dashboard-project-{ProjectId:N}']");
        Assert.Equal($"/projects?projectId={ProjectId:D}", project.GetAttribute("href"));
        Assert.Contains("Apollo", project.TextContent, StringComparison.Ordinal);
        Assert.Equal(6, cut.FindAll("a[data-testid^='dashboard-project-']").Count);
        Assert.Single(
            cut.FindComponents<Grid>(),
            grid => grid.Instance.Columns == 3);

        var workflowTab = cut.Find("[data-testid='dashboard-workflows-tab']");
        Assert.Contains("Active workflows", workflowTab.TextContent, StringComparison.Ordinal);
        var workflow = cut.Find($"[data-testid='dashboard-workflow-{WorkflowRunId:N}']");
        Assert.Equal(
            $"/agents/workflows?workflowId={WorkflowId:D}&runId={WorkflowRunId:D}",
            workflow.GetAttribute("href"));
        Assert.Contains("Release workflow", workflow.TextContent, StringComparison.Ordinal);
        Assert.DoesNotContain("Preparing release artifacts", workflow.TextContent, StringComparison.Ordinal);
        Assert.Equal("Preparing release artifacts", workflow.GetAttribute("title"));

        var processTab = cut.Find("[data-testid='dashboard-processes-tab']");
        Assert.Contains("Recent processes", processTab.TextContent, StringComparison.Ordinal);
        processTab.Click();

        cut.WaitForAssertion(() =>
        {
            var process = cut.Find($"[data-testid='dashboard-process-{ProcessRunId:N}']");
            Assert.Equal(
                $"/processes/live?runId={ProcessRunId:D}",
                process.GetAttribute("href"));
            Assert.Contains("Deploy process", process.TextContent, StringComparison.Ordinal);
        });

        var projectionLag = Assert.Single(
            cut.FindComponents<StatusBadge>(),
            badge => badge.Instance.Text == "Projection behind");
        Assert.Equal("warning", projectionLag.Instance.Tone);
    }

    [Fact]
    public async Task Manual_refresh_forces_reload_inside_cache_interval()
    {
        await using var harness = CreateHarness(
            callNumber => Task.FromResult(CreateEmptyData(
                callNumber == 1 ? 1_000 : 2_000)));
        var cut = harness.RenderHome();

        cut.WaitForAssertion(() => Assert.Equal(
            1_000L.ToString("N0", CultureInfo.CurrentCulture),
            FindStat(cut, "Total tokens").Value));

        cut.Find("[data-testid='dashboard-refresh']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, harness.Runner.LoadCount);
            Assert.Equal(
                2_000L.ToString("N0", CultureInfo.CurrentCulture),
                FindStat(cut, "Total tokens").Value);
        });
    }

    [Fact]
    public async Task Initial_failure_shows_retry_and_retry_recovers()
    {
        await using var harness = CreateHarness(
            callNumber => callNumber == 1
                ? Task.FromException<DashboardSnapshotData>(new InvalidOperationException("projects unavailable"))
                : Task.FromResult(CreateEmptyData(observedTokens: 4_200)));
        var cut = harness.RenderHome();

        cut.WaitForAssertion(() =>
        {
            var error = cut.Find("[data-testid='dashboard-load-error']");
            Assert.Contains("Dashboard snapshot unavailable", error.TextContent, StringComparison.Ordinal);
            Assert.Contains("No successful dashboard snapshot", error.TextContent, StringComparison.Ordinal);
            Assert.Empty(cut.FindComponents<CompactStat>());
        });

        cut.Find("[data-testid='dashboard-retry']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, harness.Runner.LoadCount);
            Assert.Empty(cut.FindAll("[data-testid='dashboard-load-error']"));
            Assert.Equal(
                4_200L.ToString("N0", CultureInfo.CurrentCulture),
                FindStat(cut, "Total tokens").Value);
        });
    }

    [Fact]
    public async Task Failed_refresh_keeps_stale_snapshot_visible_with_warning()
    {
        await using var harness = CreateHarness(
            callNumber => callNumber == 1
                ? Task.FromResult(CreatePopulatedData())
                : Task.FromException<DashboardSnapshotData>(new InvalidOperationException("workflow source unavailable")));
        var cut = harness.RenderHome();

        cut.WaitForElement($"[data-testid='dashboard-project-{ProjectId:N}']");
        cut.Find("[data-testid='dashboard-refresh']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, harness.Runner.LoadCount);
            var warning = cut.Find("[data-testid='dashboard-stale-warning']");
            Assert.Contains("Showing the last successful snapshot", warning.TextContent, StringComparison.Ordinal);
            Assert.Contains("Apollo", cut.Markup, StringComparison.Ordinal);
            Assert.Equal(
                12_345L.ToString("N0", CultureInfo.CurrentCulture),
                FindStat(cut, "Total tokens").Value);
        });
    }

    [Fact]
    public async Task Automatic_refresh_runs_once_at_expiry_resets_countdown_and_stops_after_disposal()
    {
        var clock = new ManualTimerTimeProvider(InitialUtc);
        var refreshInterval = TimeSpan.FromMinutes(5);
        await using var harness = CreateHarness(
            callNumber => Task.FromResult(CreateEmptyData(
                callNumber == 1 ? 1_000 : 2_000,
                clock.GetUtcNow())),
            clock,
            refreshInterval);
        var cut = harness.RenderHome();

        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, harness.Runner.LoadCount);
            Assert.Equal(1, clock.PendingTimerCount);
            Assert.Equal(
                1_000L.ToString("N0", CultureInfo.CurrentCulture),
                FindStat(cut, "Total tokens").Value);
            Assert.Equal(
                "Auto refresh in 5 min",
                cut.Find("[data-testid='dashboard-refresh-countdown']").TextContent.Trim());
        });

        clock.Advance(TimeSpan.FromMinutes(1));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(1, harness.Runner.LoadCount);
            Assert.Equal(1, clock.PendingTimerCount);
            Assert.Equal(
                "Auto refresh in 4 min",
                cut.Find("[data-testid='dashboard-refresh-countdown']").TextContent.Trim());
        });

        clock.Advance(TimeSpan.FromMinutes(4));
        cut.WaitForAssertion(() =>
        {
            Assert.Equal(2, harness.Runner.LoadCount);
            Assert.Equal(1, clock.PendingTimerCount);
            Assert.Equal(
                2_000L.ToString("N0", CultureInfo.CurrentCulture),
                FindStat(cut, "Total tokens").Value);
            Assert.Equal(
                "Auto refresh in 5 min",
                cut.Find("[data-testid='dashboard-refresh-countdown']").TextContent.Trim());
        });

        await harness.DisposeHomeAsync(cut).WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(0, clock.PendingTimerCount);

        clock.Advance(TimeSpan.FromMinutes(10));

        Assert.Equal(2, harness.Runner.LoadCount);
    }

    private static DashboardPageHarness CreateHarness(
        Func<int, Task<DashboardSnapshotData>> loadAsync,
        TimeProvider? timeProvider = null,
        TimeSpan? refreshInterval = null)
    {
        return new DashboardPageHarness(
            timeProvider ?? new FixedTimeProvider(InitialUtc),
            refreshInterval ?? DashboardSnapshotOptions.DefaultRefreshInterval,
            loadAsync);
    }

    private static void AssertQuickActions(IRenderedComponent<DashboardHome> cut)
    {
        var actions = cut.FindComponents<QuickActionCard>();
        Assert.Collection(
            actions,
            action => AssertQuickAction(
                action.Instance,
                "/projects",
                "folder_open",
                "Projects",
                "dashboard-action-projects"),
            action => AssertQuickAction(
                action.Instance,
                "/agents",
                "smart_toy",
                "Agents",
                "dashboard-action-agents"),
            action => AssertQuickAction(
                action.Instance,
                "/processes/live",
                "monitor_heart",
                "Live Processes",
                "dashboard-action-processes"),
            action => AssertQuickAction(
                action.Instance,
                "/scheduler",
                "calendar_month",
                "Scheduler",
                "dashboard-action-scheduler"));
    }

    private static void AssertQuickAction(
        QuickActionCard action,
        string expectedHref,
        string expectedIcon,
        string expectedLabel,
        string expectedTestId)
    {
        Assert.Equal(expectedHref, action.Href);
        Assert.Equal(expectedIcon, action.Icon);
        Assert.Equal(expectedLabel, action.Label);
        Assert.Equal(expectedTestId, action.TestId);
    }

    private static CompactStat FindStat(
        IRenderedComponent<DashboardHome> cut,
        string label)
    {
        return Assert.Single(
            cut.FindComponents<CompactStat>(),
            stat => stat.Instance.Label == label)
            .Instance;
    }

    private static DashboardSnapshotData CreateEmptyData(
        long observedTokens,
        DateTimeOffset? nowUtc = null)
    {
        var timestamp = nowUtc ?? InitialUtc;
        return new DashboardSnapshotData(
            Projects: [],
            WorkflowMode: DashboardActivityMode.RecentFallback,
            Workflows: [],
            ProcessMode: DashboardActivityMode.RecentFallback,
            Processes: [],
            Usage: new DashboardUsageTotals(
                observedTokens,
                KnownCostUsd: 0m,
                UnknownUsageObservationCount: 0,
                UpdatedAtUtc: timestamp));
    }

    private static DashboardSnapshotData CreatePopulatedData()
    {
        return new DashboardSnapshotData(
            Projects:
            [
                new DashboardProjectItem(
                    ProjectId,
                    "Apollo",
                    "Delivery",
                    new DashboardDisplayStatus("On track", DashboardStatusTone.Success),
                    InitialUtc.AddMinutes(-2)),
                new DashboardProjectItem(
                    Guid.Parse("55555555-5555-5555-5555-555555555555"),
                    "Beacon",
                    "Planning",
                    new DashboardDisplayStatus("Draft", DashboardStatusTone.Neutral),
                    InitialUtc.AddMinutes(-3)),
                new DashboardProjectItem(
                    Guid.Parse("66666666-6666-6666-6666-666666666666"),
                    "Cirrus",
                    "Delivery",
                    new DashboardDisplayStatus("Active", DashboardStatusTone.Success),
                    InitialUtc.AddMinutes(-4)),
                new DashboardProjectItem(
                    Guid.Parse("77777777-7777-7777-7777-777777777777"),
                    "Drift",
                    "Review",
                    new DashboardDisplayStatus("On hold", DashboardStatusTone.Warning),
                    InitialUtc.AddMinutes(-5)),
                new DashboardProjectItem(
                    Guid.Parse("88888888-8888-8888-8888-888888888888"),
                    "Ember",
                    "",
                    new DashboardDisplayStatus("Active", DashboardStatusTone.Success),
                    InitialUtc.AddMinutes(-6)),
                new DashboardProjectItem(
                    Guid.Parse("99999999-9999-9999-9999-999999999999"),
                    "Fathom",
                    "Validation",
                    new DashboardDisplayStatus("Completed", DashboardStatusTone.Success),
                    InitialUtc.AddMinutes(-7))
            ],
            WorkflowMode: DashboardActivityMode.Active,
            Workflows:
            [
                new DashboardWorkflowRunItem(
                    WorkflowId,
                    WorkflowRunId,
                    "Release workflow",
                    "Preparing release artifacts",
                    new DashboardDisplayStatus("Running", DashboardStatusTone.Info),
                    InitialUtc.AddMinutes(-1))
            ],
            ProcessMode: DashboardActivityMode.RecentFallback,
            Processes:
            [
                new DashboardProcessRunItem(
                    ProcessRunId,
                    "Deploy process",
                    "Apollo",
                    new DashboardDisplayStatus("Completed", DashboardStatusTone.Success),
                    InitialUtc.AddMinutes(-3),
                    ProjectionIsBehind: true)
            ],
            Usage: new DashboardUsageTotals(
                ObservedTokens: 12_345,
                KnownCostUsd: 0.123456m,
                UnknownUsageObservationCount: 2,
                UpdatedAtUtc: InitialUtc));
    }

    private sealed class DashboardPageHarness : IAsyncDisposable
    {
        private readonly List<IRenderedComponent<DashboardHome>> renderedHomes = [];

        public DashboardPageHarness(
            TimeProvider timeProvider,
            TimeSpan refreshInterval,
            Func<int, Task<DashboardSnapshotData>> loadAsync)
        {
            Context = new BunitContext();
            Context.JSInterop.Mode = JSRuntimeMode.Loose;
            Context.Services.AddLogging();
            Context.Services.AddCanDoItAllBaseLib();
            Context.Services.AddSingleton(timeProvider);

            Runner = new RecordingDashboardSnapshotLoadRunner(loadAsync);
            var cache = new DashboardSnapshotCache(
                new StableDatabaseRuntimeState(),
                Runner,
                timeProvider,
                Options.Create(new DashboardSnapshotOptions
                {
                    RefreshInterval = refreshInterval
                }),
                NullLogger<DashboardSnapshotCache>.Instance);
            Context.Services.AddSingleton(new DashboardSnapshotService(cache));
        }

        public BunitContext Context { get; }

        public RecordingDashboardSnapshotLoadRunner Runner { get; }

        public IRenderedComponent<DashboardHome> RenderHome()
        {
            var component = Context.Render<DashboardHome>();
            renderedHomes.Add(component);
            return component;
        }

        public async Task DisposeHomeAsync(IRenderedComponent<DashboardHome> component)
        {
            await component.Instance.DisposeAsync().AsTask();
            component.Dispose();
            renderedHomes.Remove(component);
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var component in renderedHomes.ToArray())
            {
                await component.Instance.DisposeAsync();
                component.Dispose();
            }

            renderedHomes.Clear();
            await Context.DisposeComponentsAsync();
            await Context.Services.DisposeAsync();
            Context.Dispose();
        }
    }

    private sealed class RecordingDashboardSnapshotLoadRunner(
        Func<int, Task<DashboardSnapshotData>> loadAsync) : IDashboardSnapshotLoadRunner
    {
        private int loadCount;

        public int LoadCount => Volatile.Read(ref loadCount);

        public Task<DashboardSnapshotData> LoadAsync()
        {
            var callNumber = Interlocked.Increment(ref loadCount);
            return loadAsync(callNumber);
        }
    }

    private sealed class StableDatabaseRuntimeState : IDatabaseRuntimeState
    {
        private readonly DatabaseRuntimeSnapshot snapshot = new(
            Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            "dashboard-component-test",
            Generation: 1);

        public DatabaseRuntimeSnapshot GetSnapshot()
        {
            return snapshot;
        }

        public void MarkCurrentProfile(ResolvedDatabaseProfile profile)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow()
        {
            return utcNow;
        }
    }

    private sealed class ManualTimerTimeProvider(DateTimeOffset initialUtc) : TimeProvider
    {
        private readonly object sync = new();
        private readonly List<ManualTimer> timers = [];
        private DateTimeOffset utcNow = initialUtc;

        public int PendingTimerCount
        {
            get
            {
                lock (sync)
                {
                    return timers.Count;
                }
            }
        }

        public override DateTimeOffset GetUtcNow()
        {
            lock (sync)
            {
                return utcNow;
            }
        }

        public override ITimer CreateTimer(
            TimerCallback callback,
            object? state,
            TimeSpan dueTime,
            TimeSpan period)
        {
            ArgumentNullException.ThrowIfNull(callback);

            var timer = new ManualTimer(this, callback, state);
            timer.Change(dueTime, period);
            return timer;
        }

        public void Advance(TimeSpan duration)
        {
            ArgumentOutOfRangeException.ThrowIfLessThan(duration, TimeSpan.Zero);

            ManualTimer[] dueTimers;
            lock (sync)
            {
                utcNow += duration;
                dueTimers = timers
                    .Where(timer => timer.IsDue(utcNow))
                    .ToArray();
                foreach (var timer in dueTimers)
                {
                    timer.MarkFired();
                    timers.Remove(timer);
                }
            }

            foreach (var timer in dueTimers)
            {
                timer.Invoke();
            }
        }

        private bool ChangeTimer(
            ManualTimer timer,
            TimeSpan dueTime,
            TimeSpan period)
        {
            if (dueTime < Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(dueTime));
            }

            if (period != Timeout.InfiniteTimeSpan)
            {
                throw new NotSupportedException(
                    "The dashboard test time provider supports one-shot timers only.");
            }

            lock (sync)
            {
                if (timer.IsDisposed)
                {
                    return false;
                }

                timer.DueAtUtc = dueTime == Timeout.InfiniteTimeSpan
                    ? null
                    : utcNow + dueTime;
                if (!timers.Contains(timer))
                {
                    timers.Add(timer);
                }

                return true;
            }
        }

        private void DisposeTimer(ManualTimer timer)
        {
            lock (sync)
            {
                timer.MarkDisposed();
                timers.Remove(timer);
            }
        }

        private sealed class ManualTimer(
            ManualTimerTimeProvider owner,
            TimerCallback callback,
            object? state) : ITimer
        {
            private int isDisposed;

            public DateTimeOffset? DueAtUtc { get; set; }

            public bool IsDisposed => Volatile.Read(ref isDisposed) == 1;

            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                return owner.ChangeTimer(this, dueTime, period);
            }

            public void Dispose()
            {
                owner.DisposeTimer(this);
            }

            public ValueTask DisposeAsync()
            {
                Dispose();
                return ValueTask.CompletedTask;
            }

            public bool IsDue(DateTimeOffset nowUtc)
            {
                return !IsDisposed && DueAtUtc.HasValue && DueAtUtc.Value <= nowUtc;
            }

            public void MarkFired()
            {
                DueAtUtc = null;
            }

            public void MarkDisposed()
            {
                Interlocked.Exchange(ref isDisposed, 1);
                DueAtUtc = null;
            }

            public void Invoke()
            {
                if (!IsDisposed)
                {
                    callback(state);
                }
            }
        }
    }
}
