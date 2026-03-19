using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workbench;

/* codex-capsule
kind: service
name: WorkbenchStateService
summary: Owns the internal tab session including restore, pinning, ordering, sleep, and activation rules.
owns: tab-list, active-tab, restore-report
deps: IWorkbenchStateStore, WorkbenchOptions, IClock
risks: stale-snapshot, duplicate-route, dirty-state-loss
tests: unit:WorkbenchStateServiceTests
inputs: route opens, artifact opens, tab actions
outputs: WorkbenchSessionSnapshot
state: ordered-tabs, active-tab-id, restore-report
restore: browser-storage snapshot versioned by schema
*/
public sealed class WorkbenchStateService(
    IWorkbenchStateStore stateStore,
    IOptions<WorkbenchOptions> options,
    IClock clock)
{
    private readonly List<WorkbenchTabState> _tabs = [];
    private readonly WorkbenchOptions _options = options.Value;
    private bool _initialized;

    public event Action? Changed;

    public IReadOnlyList<WorkbenchTabState> Tabs => _tabs.OrderBy(tab => tab.Order).ToList();

    public string? ActiveTabId { get; private set; }

    public WorkbenchRestoreReport? LastRestoreReport { get; private set; }

    public async Task InitializeAsync(IEnumerable<WorkbenchTabState> defaultTabs, CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        var snapshot = await stateStore.LoadAsync(cancellationToken);
        var failures = new List<WorkbenchRestoreFailure>();
        if (snapshot?.Tabs.Count > 0)
        {
            foreach (var tab in snapshot.Tabs.OrderBy(tab => tab.Order))
            {
                if (string.IsNullOrWhiteSpace(tab.TabId) || string.IsNullOrWhiteSpace(tab.Route))
                {
                    failures.Add(new WorkbenchRestoreFailure(tab.TabId, tab.Title, "Tab route or id was missing."));
                    continue;
                }

                if (_tabs.Any(existing => string.Equals(existing.TabId, tab.TabId, StringComparison.Ordinal)))
                {
                    failures.Add(new WorkbenchRestoreFailure(tab.TabId, tab.Title, "Duplicate tab id found in restore snapshot."));
                    continue;
                }

                _tabs.Add(tab with { LastActivatedAtUtc = tab.LastActivatedAtUtc ?? clock.GetUtcNow() });
            }
        }

        if (_tabs.Count == 0)
        {
            _tabs.Clear();
            _tabs.AddRange(defaultTabs.Select((tab, index) => tab with { Order = index, LastActivatedAtUtc = clock.GetUtcNow() }));
        }

        ActiveTabId = snapshot?.ActiveTabId;
        if (string.IsNullOrWhiteSpace(ActiveTabId) || _tabs.All(tab => !string.Equals(tab.TabId, ActiveTabId, StringComparison.Ordinal)))
        {
            ActiveTabId = _tabs.FirstOrDefault()?.TabId;
        }

        LastRestoreReport = new WorkbenchRestoreReport(clock.GetUtcNow(), _tabs.Count, failures);
        EnsureActiveTabIsAwake();
        AutoSleepBackgroundTabs();
        _initialized = true;
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task TrackRouteAsync(
        string route,
        string title,
        bool pinned,
        Guid? projectId = null,
        string tabKind = "page",
        CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeRoute(route);
        var existing = _tabs.FirstOrDefault(tab => string.Equals(tab.Route, normalized, StringComparison.OrdinalIgnoreCase));
        if (existing is null)
        {
            _tabs.Add(new WorkbenchTabState(
                $"route:{normalized}",
                title,
                normalized,
                IsPinned: pinned,
                CanClose: !pinned,
                ProjectScope: projectId?.ToString(),
                TabKind: tabKind,
                ProjectId: projectId,
                RestoreKey: normalized,
                LastActivatedAtUtc: clock.GetUtcNow(),
                Order: _tabs.Count));
        }
        else
        {
            ReplaceTab(existing with
            {
                Title = title,
                IsPinned = existing.IsPinned || pinned,
                CanClose = !(existing.IsPinned || pinned),
                IsSleeping = false,
                LastActivatedAtUtc = clock.GetUtcNow()
            });
        }

        ActiveTabId = _tabs.First(tab => string.Equals(tab.Route, normalized, StringComparison.OrdinalIgnoreCase)).TabId;
        EnsureActiveTabIsAwake();
        AutoSleepBackgroundTabs();
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public Task OpenArtifactAsync(ArtifactReference artifactReference, CancellationToken cancellationToken = default)
        => TrackRouteAsync(artifactReference.Route, artifactReference.Title, false, artifactReference.EntityId, artifactReference.Kind, cancellationToken);

    public async Task ActivateAsync(string tabId, CancellationToken cancellationToken = default)
    {
        var tab = _tabs.FirstOrDefault(candidate => string.Equals(candidate.TabId, tabId, StringComparison.Ordinal));
        if (tab is null)
        {
            return;
        }

        ActiveTabId = tabId;
        ReplaceTab(tab with { IsSleeping = false, LastActivatedAtUtc = clock.GetUtcNow() });
        EnsureActiveTabIsAwake();
        AutoSleepBackgroundTabs();
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task CloseAsync(string tabId, CancellationToken cancellationToken = default)
    {
        var tab = _tabs.FirstOrDefault(candidate => string.Equals(candidate.TabId, tabId, StringComparison.Ordinal));
        if (tab is null || tab.IsPinned)
        {
            return;
        }

        _tabs.Remove(tab);
        Reindex();
        if (string.Equals(ActiveTabId, tabId, StringComparison.Ordinal))
        {
            ActiveTabId = _tabs.LastOrDefault()?.TabId;
        }

        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task TogglePinAsync(string tabId, CancellationToken cancellationToken = default)
    {
        var tab = _tabs.FirstOrDefault(candidate => string.Equals(candidate.TabId, tabId, StringComparison.Ordinal));
        if (tab is null)
        {
            return;
        }

        ReplaceTab(tab with { IsPinned = !tab.IsPinned, CanClose = tab.IsPinned });
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task ToggleSleepAsync(string tabId, CancellationToken cancellationToken = default)
    {
        var tab = _tabs.FirstOrDefault(candidate => string.Equals(candidate.TabId, tabId, StringComparison.Ordinal));
        if (tab is null || !tab.CanSleep)
        {
            return;
        }

        if (string.Equals(tab.TabId, ActiveTabId, StringComparison.Ordinal) && !tab.IsSleeping)
        {
            return;
        }

        ReplaceTab(tab with { IsSleeping = !tab.IsSleeping });
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task MoveAsync(string tabId, int direction, CancellationToken cancellationToken = default)
    {
        var ordered = Tabs.ToList();
        var index = ordered.FindIndex(tab => string.Equals(tab.TabId, tabId, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        var targetIndex = Math.Clamp(index + direction, 0, ordered.Count - 1);
        if (targetIndex == index)
        {
            return;
        }

        var moved = ordered[index];
        ordered.RemoveAt(index);
        ordered.Insert(targetIndex, moved);

        _tabs.Clear();
        _tabs.AddRange(ordered.Select((tab, newIndex) => tab with { Order = newIndex }));
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task MarkDirtyAsync(string tabId, bool isDirty, CancellationToken cancellationToken = default)
    {
        var tab = _tabs.FirstOrDefault(candidate => string.Equals(candidate.TabId, tabId, StringComparison.Ordinal));
        if (tab is null)
        {
            return;
        }

        ReplaceTab(tab with { IsDirty = isDirty });
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public WorkbenchTabState? GetActiveTab()
        => _tabs.FirstOrDefault(tab => string.Equals(tab.TabId, ActiveTabId, StringComparison.Ordinal));

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        var snapshot = new WorkbenchSessionSnapshot(
            2,
            ActiveTabId,
            Tabs.ToArray(),
            CompatibilityMarker: "candoitall.workbench.v2",
            SavedAtUtc: clock.GetUtcNow(),
            RestoreFailures: LastRestoreReport?.Failures);

        await stateStore.SaveAsync(snapshot, cancellationToken);
    }

    private void EnsureActiveTabIsAwake()
    {
        var active = GetActiveTab();
        if (active is null || !active.IsSleeping)
        {
            return;
        }

        ReplaceTab(active with { IsSleeping = false, LastActivatedAtUtc = clock.GetUtcNow() });
    }

    private void AutoSleepBackgroundTabs()
    {
        var ordered = Tabs.ToList();
        var activeTabId = ActiveTabId;
        var warmCount = 0;

        foreach (var tab in ordered)
        {
            var shouldRemainWarm = string.Equals(tab.TabId, activeTabId, StringComparison.Ordinal) || tab.IsPinned;
            if (shouldRemainWarm)
            {
                warmCount++;
                if (tab.IsSleeping)
                {
                    ReplaceTab(tab with { IsSleeping = false });
                }

                continue;
            }

            var idleMinutes = tab.LastActivatedAtUtc.HasValue
                ? (clock.GetUtcNow() - tab.LastActivatedAtUtc.Value).TotalMinutes
                : _options.SleepAfterMinutes + 1;
            var shouldSleep = warmCount >= _options.MaxWarmTabs || idleMinutes >= _options.SleepAfterMinutes;

            ReplaceTab(tab with { IsSleeping = shouldSleep && tab.CanSleep });
            warmCount += shouldSleep ? 0 : 1;
        }
    }

    private void Reindex()
    {
        for (var index = 0; index < _tabs.Count; index++)
        {
            _tabs[index] = _tabs[index] with { Order = index };
        }
    }

    private void ReplaceTab(WorkbenchTabState updatedTab)
    {
        var index = _tabs.FindIndex(tab => string.Equals(tab.TabId, updatedTab.TabId, StringComparison.Ordinal));
        if (index >= 0)
        {
            _tabs[index] = updatedTab;
        }
    }

    private void NotifyStateChanged() => Changed?.Invoke();

    private static string NormalizeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return "/";
        }

        return route.StartsWith('/') ? route : $"/{route}";
    }
}

public sealed class InMemoryWorkbenchStateStore : IWorkbenchStateStore
{
    private WorkbenchSessionSnapshot? _snapshot;

    public ValueTask<WorkbenchSessionSnapshot?> LoadAsync(CancellationToken cancellationToken = default)
        => ValueTask.FromResult(_snapshot);

    public ValueTask SaveAsync(WorkbenchSessionSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        _snapshot = snapshot with
        {
            Tabs = snapshot.Tabs
                .Select(tab => tab with { })
                .ToArray()
        };

        return ValueTask.CompletedTask;
    }
}
