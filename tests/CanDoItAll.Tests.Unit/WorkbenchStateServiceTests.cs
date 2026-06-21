using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit;

public sealed class WorkbenchStateServiceTests
{
    [Fact]
    public async Task InitializeAsync_uses_defaults_and_auto_sleeps_background_tabs()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore
        {
            Snapshot = new WorkbenchSessionSnapshot(
                2,
                "missing",
                [
                    new WorkbenchTabState(string.Empty, "Broken", string.Empty),
                    new WorkbenchTabState("missing-route", "Missing route", string.Empty),
                    new WorkbenchTabState("duplicate", "Second", string.Empty),
                    new WorkbenchTabState("duplicate", "Duplicate", string.Empty)
                ])
        };

        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 1, SleepAfterMinutes = 5 }),
            clock);

        await service.InitializeAsync(
            [
                new WorkbenchTabState("dashboard", "Dashboard", "/"),
                new WorkbenchTabState("projects", "Projects", "/projects", LastActivatedAtUtc: clock.GetUtcNow().AddMinutes(-20))
            ]);

        Assert.Equal("dashboard", service.ActiveTabId);
        Assert.Equal(2, service.Tabs.Count);
        Assert.NotNull(service.LastRestoreReport);
        Assert.Equal(4, service.LastRestoreReport!.Failures.Count);
        Assert.False(service.Tabs.First(tab => tab.TabId == "dashboard").IsSleeping);
        Assert.True(service.Tabs.First(tab => tab.TabId == "projects").IsSleeping);
        Assert.NotNull(store.SavedSnapshot);
        Assert.Equal("candoitall.workbench.v4", store.SavedSnapshot!.CompatibilityMarker);
    }

    [Fact]
    public async Task TrackRouteAsync_reuses_existing_tab_and_updates_title()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore();
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard", "Dashboard", "/")]);
        await service.TrackRouteAsync("/test-lab", "Test Lab", false);
        await service.TrackRouteAsync("/test-lab", "Test Evidence", false);

        var testLabTabs = service.Tabs.Where(tab => tab.Route == "/test-lab").ToList();
        Assert.Single(testLabTabs);
        Assert.Equal("Test Evidence", testLabTabs[0].Title);
        Assert.Equal(testLabTabs[0].TabId, service.ActiveTabId);
        Assert.False(testLabTabs[0].IsSleeping);
    }

    [Fact]
    public async Task InitializeAsync_drops_retired_module_routes_from_restored_session()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore
        {
            Snapshot = new WorkbenchSessionSnapshot(
                4,
                "route:validation",
                [
                    new WorkbenchTabState("dashboard", "Dashboard", "/", Order: 0),
                    new WorkbenchTabState("route:validation", "Validation Center", "/validation", Order: 1),
                    new WorkbenchTabState("route:automation", "Automation", "/automation", Order: 2)
                ],
                CompatibilityMarker: "candoitall.workbench.v4",
                RecentTabs:
                [
                    new WorkbenchTabState("route:activity", "Activity", "/activity")
                ])
        };
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard-default", "Dashboard", "/")]);
        await service.TrackRouteAsync("/validation", "Validation Center", false);

        Assert.Single(service.Tabs);
        Assert.Equal("dashboard", service.ActiveTabId);
        Assert.DoesNotContain(service.Tabs, tab => tab.Route is "/validation" or "/activity" or "/automation");
        Assert.Empty(service.RecentTabs);
        Assert.NotNull(service.LastRestoreReport);
        Assert.Equal(2, service.LastRestoreReport!.Failures.Count);
        Assert.All(
            service.LastRestoreReport.Failures,
            failure => Assert.Contains("retired module", failure.Summary, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(store.SavedSnapshot);
        Assert.DoesNotContain(store.SavedSnapshot!.Tabs, tab => tab.Route is "/validation" or "/activity" or "/automation");
    }

    [Fact]
    public async Task InitializeAsync_ignores_snapshots_with_an_incompatible_compatibility_marker()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore
        {
            Snapshot = new WorkbenchSessionSnapshot(
                3,
                "dashboard",
                [new WorkbenchTabState("legacy", "Legacy", "/legacy")],
                CompatibilityMarker: "candoitall.workbench.v3")
        };
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard", "Dashboard", "/")]);

        Assert.Single(service.Tabs);
        Assert.Equal("dashboard", service.Tabs[0].TabId);
        Assert.NotNull(service.LastRestoreReport);
        Assert.Single(service.LastRestoreReport!.Failures);
        Assert.Contains("incompatible session format", service.LastRestoreReport.Failures[0].Summary, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class TestWorkbenchStateStore : IWorkbenchStateStore
    {
        public WorkbenchSessionSnapshot? Snapshot { get; init; }

        public WorkbenchSessionSnapshot? SavedSnapshot { get; private set; }

        public ValueTask<WorkbenchSessionSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
            => ValueTask.FromResult(Snapshot);

        public ValueTask SaveAsync(WorkbenchSessionSnapshot snapshot, CancellationToken cancellationToken = default)
        {
            SavedSnapshot = snapshot;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TestClock(DateTimeOffset currentUtc) : IClock
    {
        public DateTimeOffset GetUtcNow() => currentUtc;
    }
}
