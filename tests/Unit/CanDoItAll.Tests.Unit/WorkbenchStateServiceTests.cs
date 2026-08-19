using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Tests.Unit.Infrastructure;

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

    [Fact]
    public async Task InitializeAsync_deduplicates_restored_tabs_by_artifact_identity()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore
        {
            Snapshot = new WorkbenchSessionSnapshot(
                4,
                "route:processes",
                [
                    new WorkbenchTabState(
                        "route:processes",
                        "Processes",
                        "/processes",
                        TabKind: WorkbenchTabKinds.Processes,
                        ArtifactKey: "process-workspace",
                        RestoreKey: "process-workspace",
                        Order: 0),
                    new WorkbenchTabState(
                        "route:processes-copy",
                        "Processes copy",
                        "/processes?unused=true",
                        TabKind: WorkbenchTabKinds.Processes,
                        ArtifactKey: "process-workspace",
                        RestoreKey: "process-workspace",
                        Order: 1)
                ],
                CompatibilityMarker: "candoitall.workbench.v4")
        };
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard", "Dashboard", "/")]);

        Assert.Single(service.Tabs);
        Assert.Equal("route:processes", service.ActiveTabId);
        Assert.NotNull(service.LastRestoreReport);
        Assert.Single(service.LastRestoreReport!.Failures);
        Assert.Contains("Duplicate tab", service.LastRestoreReport.Failures[0].Summary);
        Assert.Single(store.SavedSnapshot!.Tabs);
    }

    [Fact]
    public async Task InitializeAsync_deduplicates_restored_project_structure_tabs_by_route_identity()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var projectId = Guid.NewGuid();
        var route = $"/projects/{projectId:D}/structure";
        var canonicalArtifactKey = $"project-structure:{projectId:N}";
        var store = new TestWorkbenchStateStore
        {
            Snapshot = new WorkbenchSessionSnapshot(
                4,
                $"project-structure:{projectId:N}",
                [
                    new WorkbenchTabState(
                        "legacy-project-structure",
                        "Legacy Structure",
                        route,
                        TabKind: WorkbenchTabKinds.ProjectStructure,
                        ProjectId: projectId,
                        ArtifactKey: "project-child:legacy-node",
                        RestoreKey: "project-child:legacy-node",
                        Order: 0),
                    new WorkbenchTabState(
                        $"project-structure:{projectId:N}",
                        "Canonical Structure",
                        route,
                        TabKind: WorkbenchTabKinds.ProjectStructure,
                        ProjectId: projectId,
                        ArtifactKey: canonicalArtifactKey,
                        RestoreKey: canonicalArtifactKey,
                        Order: 1)
                ],
                CompatibilityMarker: "candoitall.workbench.v4")
        };
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard", "Dashboard", "/")]);

        Assert.Single(service.Tabs, tab => string.Equals(tab.Route, route, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("legacy-project-structure", service.ActiveTabId);
        Assert.NotNull(service.LastRestoreReport);
        Assert.Single(service.LastRestoreReport!.Failures);
        Assert.Contains("Duplicate tab", service.LastRestoreReport.Failures[0].Summary);
        Assert.Single(store.SavedSnapshot!.Tabs);
    }

    [Fact]
    public async Task TrackTabAsync_reuses_same_page_tab_by_restore_identity()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore();
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard", "Dashboard", "/")]);
        await service.TrackTabAsync(new WorkbenchTabDescriptor(
            "route:processes",
            "Processes",
            "/processes",
            WorkbenchTabKinds.Processes,
            ArtifactKind: "process-workspace",
            ArtifactKey: "process-workspace",
            RestoreKey: "process-workspace"));
        await service.TrackTabAsync(new WorkbenchTabDescriptor(
            "route:processes-duplicate",
            "Processes",
            "/processes?ignored=true",
            WorkbenchTabKinds.Processes,
            ArtifactKind: "process-workspace",
            ArtifactKey: "process-workspace",
            RestoreKey: "process-workspace"));

        var processTabs = service.Tabs.Where(tab => string.Equals(tab.RestoreKey, "process-workspace", StringComparison.Ordinal)).ToList();
        Assert.Single(processTabs);
        Assert.Equal("route:processes", processTabs[0].TabId);
        Assert.Equal("/processes?ignored=true", processTabs[0].Route);
    }

    [Fact]
    public async Task TrackTabAsync_reuses_project_structure_tab_by_route_when_artifact_keys_differ()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore();
        var projectId = Guid.NewGuid();
        var route = $"/projects/{projectId:D}/structure";
        var canonicalArtifactKey = $"project-structure:{projectId:N}";
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard", "Dashboard", "/")]);
        await service.TrackTabAsync(new WorkbenchTabDescriptor(
            "legacy-project-structure",
            "Legacy Structure",
            route,
            WorkbenchTabKinds.ProjectStructure,
            ProjectId: projectId,
            ArtifactKind: "project-child",
            ArtifactKey: "project-child:legacy-node",
            RestoreKey: "project-child:legacy-node"));
        await service.TrackTabAsync(new WorkbenchTabDescriptor(
            $"project-structure:{projectId:N}",
            "Canonical Structure",
            route,
            WorkbenchTabKinds.ProjectStructure,
            ProjectId: projectId,
            ArtifactId: projectId,
            ArtifactKind: "project-structure",
            ArtifactKey: canonicalArtifactKey,
            RestoreKey: canonicalArtifactKey));

        var structureTabs = service.Tabs
            .Where(tab => string.Equals(tab.Route, route, StringComparison.OrdinalIgnoreCase))
            .ToList();
        Assert.Single(structureTabs);
        Assert.Equal("legacy-project-structure", structureTabs[0].TabId);
        Assert.Equal("Canonical Structure", structureTabs[0].Title);
        Assert.Equal(canonicalArtifactKey, structureTabs[0].ArtifactKey);
        Assert.Equal("legacy-project-structure", service.ActiveTabId);
    }

    [Fact]
    public async Task TrackTabAsync_removes_recent_project_structure_duplicate_by_route_identity()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore();
        var projectId = Guid.NewGuid();
        var route = $"/projects/{projectId:D}/structure";
        var canonicalArtifactKey = $"project-structure:{projectId:N}";
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard", "Dashboard", "/")]);
        await service.TrackTabAsync(new WorkbenchTabDescriptor(
            "legacy-project-structure",
            "Legacy Structure",
            route,
            WorkbenchTabKinds.ProjectStructure,
            ProjectId: projectId,
            ArtifactKind: "project-child",
            ArtifactKey: "project-child:legacy-node",
            RestoreKey: "project-child:legacy-node"));
        await service.CloseAsync("legacy-project-structure");
        await service.TrackTabAsync(new WorkbenchTabDescriptor(
            $"project-structure:{projectId:N}",
            "Canonical Structure",
            route,
            WorkbenchTabKinds.ProjectStructure,
            ProjectId: projectId,
            ArtifactId: projectId,
            ArtifactKind: "project-structure",
            ArtifactKey: canonicalArtifactKey,
            RestoreKey: canonicalArtifactKey));

        Assert.Empty(service.RecentTabs);
        Assert.Single(service.Tabs, tab => string.Equals(tab.Route, route, StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(store.SavedSnapshot);
        Assert.Empty(store.SavedSnapshot!.RecentTabs ?? []);
    }

    [Fact]
    public async Task CloseTabsAsync_removes_requested_tabs_and_repairs_active_tab()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore();
        var projectId = Guid.NewGuid();
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync(
            [
                new WorkbenchTabState("dashboard", "Dashboard", "/"),
                new WorkbenchTabState(
                    "project-structure",
                    "Deleted Project · Structure",
                    $"/projects/{projectId:D}/structure",
                    IsPinned: true,
                    ProjectId: projectId,
                    TabKind: WorkbenchTabKinds.ProjectStructure,
                    ArtifactKey: $"project-structure:{projectId:N}",
                    RestoreKey: $"project-structure:{projectId:N}",
                    Order: 1)
            ]);
        await service.ActivateAsync("project-structure");

        await service.CloseTabsAsync(["project-structure"]);

        Assert.Equal("dashboard", service.ActiveTabId);
        Assert.DoesNotContain(service.Tabs, tab => tab.TabId == "project-structure");
        Assert.Empty(service.RecentTabs);
        Assert.NotNull(store.SavedSnapshot);
        Assert.DoesNotContain(store.SavedSnapshot!.Tabs, tab => tab.TabId == "project-structure");
    }

    [Fact]
    public async Task CloseAsync_keeps_only_the_latest_recent_tabs()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore();
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard", "Dashboard", "/", IsPinned: true)]);
        for (var index = 0; index < WorkbenchTabHistoryPolicy.RecentTabCapacity + 3; index++)
        {
            await service.TrackRouteAsync($"/page-{index}", $"Page {index}", false);
            await service.CloseAsync(service.ActiveTabId!);
        }

        Assert.Equal(WorkbenchTabHistoryPolicy.RecentTabCapacity, service.RecentTabs.Count);
        Assert.Equal("Page 14", service.RecentTabs[0].Title);
        Assert.Equal("Page 3", service.RecentTabs[^1].Title);
    }

    [Fact]
    public async Task InitializeAsync_trims_oversized_recent_tab_history()
    {
        var now = new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero);
        var clock = new TestClock(now);
        var recentTabs = Enumerable.Range(0, WorkbenchTabHistoryPolicy.RecentTabCapacity + 3)
            .OrderBy(index => index % 4)
            .ThenByDescending(index => index)
            .Select(index => new WorkbenchTabState(
                $"recent-{index}",
                $"Recent {index}",
                $"/recent-{index}",
                ClosedAtUtc: now.AddMinutes(-index)))
            .ToArray();
        var store = new TestWorkbenchStateStore
        {
            Snapshot = new WorkbenchSessionSnapshot(
                4,
                "dashboard",
                [new WorkbenchTabState("dashboard", "Dashboard", "/")],
                CompatibilityMarker: "candoitall.workbench.v4",
                RecentTabs: recentTabs)
        };
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard-default", "Dashboard", "/")]);

        Assert.Equal(WorkbenchTabHistoryPolicy.RecentTabCapacity, service.RecentTabs.Count);
        Assert.Equal("recent-0", service.RecentTabs[0].TabId);
        Assert.Equal($"recent-{WorkbenchTabHistoryPolicy.RecentTabCapacity - 1}", service.RecentTabs[^1].TabId);
        Assert.Equal(WorkbenchTabHistoryPolicy.RecentTabCapacity, store.SavedSnapshot!.RecentTabs!.Count);
    }

    [Fact]
    public async Task ClearRecentTabsAsync_clears_and_persists_history()
    {
        var clock = new TestClock(new DateTimeOffset(2026, 3, 19, 12, 0, 0, TimeSpan.Zero));
        var store = new TestWorkbenchStateStore();
        var service = new WorkbenchStateService(
            store,
            Options.Create(new WorkbenchOptions { MaxWarmTabs = 3, SleepAfterMinutes = 15 }),
            clock);

        await service.InitializeAsync([new WorkbenchTabState("dashboard", "Dashboard", "/", IsPinned: true)]);
        await service.TrackRouteAsync("/projects", "Projects", false);
        await service.CloseAsync(service.ActiveTabId!);
        Assert.Single(service.RecentTabs);
        var changeCount = 0;
        service.Changed += () => changeCount++;

        await service.ClearRecentTabsAsync();

        Assert.Empty(service.RecentTabs);
        Assert.Empty(store.SavedSnapshot!.RecentTabs ?? []);
        Assert.Equal(1, changeCount);
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
