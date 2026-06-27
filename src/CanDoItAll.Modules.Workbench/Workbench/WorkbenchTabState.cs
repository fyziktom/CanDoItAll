using CanDoItAll.Infrastructure.Configuration;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Modules.Workbench;

/* codex-capsule
kind: service
name: WorkbenchStateService
summary: Owns the internal tab session including restore, pinning, ordering, sleep, snapshots, overflow, and artifact-first activation rules.
owns: tab-list, recent-tabs, active-tab, restore-report
deps: IWorkbenchStateStore, WorkbenchOptions, IClock
risks: stale-snapshot, duplicate-artifact, dirty-state-loss
tests: unit:WorkbenchStateServiceTests
inputs: route opens, artifact opens, tab actions
outputs: WorkbenchSessionSnapshot
state: ordered-tabs, recent-tabs, active-tab-id, restore-report
restore: browser-storage snapshot versioned by schema
*/
public sealed class WorkbenchStateService(
    IWorkbenchStateStore stateStore,
    IOptions<WorkbenchOptions> options,
    IClock clock)
{
    private const int SnapshotVersion = 4;
    private const string CompatibilityMarker = "candoitall.workbench.v4";
    private const int MaxRecentTabs = 12;
    private static readonly string[] RetiredModuleRoutes =
    [
        "/activity",
        "/automation",
        "/validation"
    ];

    private readonly List<WorkbenchTabState> _tabs = [];
    private readonly List<WorkbenchTabState> _recentTabs = [];
    private readonly WorkbenchOptions _options = options.Value;
    private bool _initialized;

    public event Action? Changed;

    public IReadOnlyList<WorkbenchTabState> Tabs => _tabs.OrderBy(tab => tab.Order).ToList();

    public IReadOnlyList<WorkbenchTabState> RecentTabs => _recentTabs.ToList();

    public string? ActiveTabId { get; private set; }

    public WorkbenchRestoreReport? LastRestoreReport { get; private set; }

    public bool HasDirtyTabs => _tabs.Any(tab => tab.IsDirty);

    public async Task InitializeAsync(IEnumerable<WorkbenchTabState> defaultTabs, CancellationToken cancellationToken = default)
    {
        if (_initialized)
        {
            return;
        }

        var snapshot = await stateStore.LoadAsync(cancellationToken);
        var failures = new List<WorkbenchRestoreFailure>();

        if (snapshot is not null &&
            !string.IsNullOrWhiteSpace(snapshot.CompatibilityMarker) &&
            !string.Equals(snapshot.CompatibilityMarker, CompatibilityMarker, StringComparison.Ordinal))
        {
            failures.Add(new WorkbenchRestoreFailure(
                "snapshot",
                "Workbench session",
                "Stored workbench state was ignored because it was created by an incompatible session format."));
            snapshot = null;
        }

        if (snapshot?.Tabs.Count > 0)
        {
            var restoredTabIdentities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tab in snapshot.Tabs.OrderBy(tab => tab.Order))
            {
                if (!TryNormalizeRestoredTab(tab, out var normalized, out var failure))
                {
                    failures.Add(failure!);
                    continue;
                }

                var identity = ResolveTabIdentity(normalized);
                if (_tabs.Any(existing => IsSameLogicalTab(existing, normalized)) ||
                    !restoredTabIdentities.Add(identity))
                {
                    failures.Add(new WorkbenchRestoreFailure(normalized.TabId, normalized.Title, "Duplicate tab found in restore snapshot."));
                    continue;
                }

                _tabs.Add(normalized);
            }
        }

        if (snapshot?.RecentTabs is { Count: > 0 })
        {
            foreach (var recentTab in snapshot.RecentTabs)
            {
                if (!TryNormalizeRestoredTab(recentTab, out var normalized, out _))
                {
                    continue;
                }

                if (_recentTabs.Any(existing => IsSameLogicalTab(existing, normalized)) ||
                    _tabs.Any(existing => IsSameLogicalTab(existing, normalized)))
                {
                    continue;
                }

                _recentTabs.Add(normalized with { ClosedAtUtc = recentTab.ClosedAtUtc ?? clock.GetUtcNow() });
            }
        }

        if (_tabs.Count == 0)
        {
            _tabs.AddRange(defaultTabs.Select((tab, index) => NormalizeNewTab(tab with
            {
                Order = index,
                LastActivatedAtUtc = tab.LastActivatedAtUtc ?? clock.GetUtcNow()
            })));
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

    public Task TrackRouteAsync(
        string route,
        string title,
        bool pinned,
        Guid? projectId = null,
        string tabKind = WorkbenchTabKinds.Page,
        CancellationToken cancellationToken = default)
    {
        if (IsRetiredModuleRoute(route))
        {
            return Task.CompletedTask;
        }

        return TrackTabAsync(
            new WorkbenchTabDescriptor(
                BuildPageTabId(route),
                title,
                route,
                tabKind,
                ProjectId: projectId,
                ArtifactId: projectId,
                ArtifactKind: tabKind,
                ArtifactKey: NormalizeRoute(route),
                RestoreKey: NormalizeRoute(route),
                ProjectScope: projectId?.ToString(),
                TabGroup: ResolveTabGroup(tabKind),
                IsPinned: pinned,
                CanClose: !pinned),
            cancellationToken);
    }

    public Task OpenArtifactAsync(ArtifactReference artifactReference, CancellationToken cancellationToken = default)
    {
        var tabKind = string.IsNullOrWhiteSpace(artifactReference.TabKind)
            ? artifactReference.Kind
            : artifactReference.TabKind;
        var route = NormalizeRoute(artifactReference.Route);
        var artifactKey = string.IsNullOrWhiteSpace(artifactReference.ArtifactKey)
            ? $"{tabKind}:{artifactReference.EntityId?.ToString("N") ?? route}"
            : artifactReference.ArtifactKey;

        return TrackTabAsync(
            new WorkbenchTabDescriptor(
                BuildArtifactTabId(tabKind, artifactReference.EntityId, route),
                artifactReference.Title,
                route,
                tabKind,
                ProjectId: artifactReference.ProjectId,
                ArtifactId: artifactReference.EntityId,
                ArtifactKind: artifactReference.Kind,
                ArtifactKey: artifactKey,
                RestoreKey: artifactKey,
                ProjectScope: artifactReference.ProjectId?.ToString(),
                ProjectName: artifactReference.ProjectName,
                PhaseName: artifactReference.PhaseName,
                Description: artifactReference.Description,
                SnapshotJson: artifactReference.SnapshotJson,
                TabGroup: ResolveTabGroup(tabKind)),
            cancellationToken);
    }

    public async Task TrackTabAsync(WorkbenchTabDescriptor descriptor, CancellationToken cancellationToken = default)
    {
        var normalizedRoute = NormalizeRoute(descriptor.Route);
        var normalizedArtifactKey = string.IsNullOrWhiteSpace(descriptor.ArtifactKey)
            ? $"{descriptor.TabKind}:{normalizedRoute}"
            : descriptor.ArtifactKey;
        var normalizedRestoreKey = descriptor.RestoreKey ?? normalizedArtifactKey;
        var candidateTab = NormalizeNewTab(new WorkbenchTabState(
            descriptor.TabId,
            descriptor.Title,
            normalizedRoute,
            IsPinned: descriptor.IsPinned,
            CanClose: descriptor.CanClose,
            ProjectScope: descriptor.ProjectScope ?? descriptor.ProjectId?.ToString(),
            TabKind: descriptor.TabKind,
            ProjectId: descriptor.ProjectId,
            RestoreKey: normalizedRestoreKey,
            CanSleep: descriptor.CanSleep,
            CapsuleKey: descriptor.CapsuleKey,
            Description: descriptor.Description,
            SnapshotJson: descriptor.SnapshotJson,
            ArtifactKey: normalizedArtifactKey,
            ArtifactKind: descriptor.ArtifactKind,
            ArtifactId: descriptor.ArtifactId,
            ProjectName: descriptor.ProjectName,
            PhaseName: descriptor.PhaseName,
            TabGroup: descriptor.TabGroup ?? ResolveTabGroup(descriptor.TabKind),
            LastActivatedAtUtc: clock.GetUtcNow(),
            Order: _tabs.Count));

        var existing = _tabs.FirstOrDefault(tab => IsSameLogicalTab(tab, candidateTab));

        if (existing is null)
        {
            _tabs.Add(candidateTab);
            RemoveMatchingRecent(candidateTab);
            ActiveTabId = candidateTab.TabId;
        }
        else
        {
            RemoveMatchingRecent(candidateTab);
            ReplaceTab(existing with
            {
                Title = descriptor.Title,
                Route = normalizedRoute,
                IsPinned = existing.IsPinned || descriptor.IsPinned,
                CanClose = !(existing.IsPinned || descriptor.IsPinned),
                IsSleeping = false,
                ProjectScope = descriptor.ProjectScope ?? existing.ProjectScope ?? descriptor.ProjectId?.ToString(),
                TabKind = descriptor.TabKind,
                ProjectId = descriptor.ProjectId ?? existing.ProjectId,
                RestoreKey = normalizedRestoreKey,
                CanSleep = descriptor.CanSleep,
                CapsuleKey = descriptor.CapsuleKey ?? existing.CapsuleKey,
                Description = descriptor.Description ?? existing.Description,
                SnapshotJson = descriptor.SnapshotJson ?? existing.SnapshotJson,
                ArtifactKey = normalizedArtifactKey,
                ArtifactKind = descriptor.ArtifactKind ?? existing.ArtifactKind,
                ArtifactId = descriptor.ArtifactId ?? existing.ArtifactId,
                ProjectName = descriptor.ProjectName ?? existing.ProjectName,
                PhaseName = descriptor.PhaseName ?? existing.PhaseName,
                TabGroup = descriptor.TabGroup ?? existing.TabGroup ?? ResolveTabGroup(descriptor.TabKind),
                LastActivatedAtUtc = clock.GetUtcNow()
            });

            ActiveTabId = existing.TabId;
        }

        EnsureActiveTabIsAwake();
        AutoSleepBackgroundTabs();
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task CloseTabsAsync(
        IEnumerable<string> tabIds,
        bool rememberRecent = false,
        CancellationToken cancellationToken = default)
    {
        var requestedTabIds = tabIds
            .Where(tabId => !string.IsNullOrWhiteSpace(tabId))
            .ToHashSet(StringComparer.Ordinal);
        if (requestedTabIds.Count == 0)
        {
            return;
        }

        var removedAny = false;
        foreach (var tab in _tabs.ToList())
        {
            if (!requestedTabIds.Contains(tab.TabId))
            {
                continue;
            }

            _tabs.Remove(tab);
            if (rememberRecent)
            {
                RememberRecentTab(tab);
            }

            removedAny = true;
        }

        foreach (var recentTab in _recentTabs.ToList())
        {
            if (!rememberRecent && requestedTabIds.Contains(recentTab.TabId))
            {
                _recentTabs.Remove(recentTab);
            }
        }

        if (!removedAny)
        {
            return;
        }

        Reindex();
        if (string.IsNullOrWhiteSpace(ActiveTabId) ||
            _tabs.All(tab => !string.Equals(tab.TabId, ActiveTabId, StringComparison.Ordinal)))
        {
            ActiveTabId = _tabs.FirstOrDefault()?.TabId;
        }

        EnsureActiveTabIsAwake();
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task ActivateAsync(string tabId, CancellationToken cancellationToken = default)
    {
        var tab = _tabs.FirstOrDefault(candidate => string.Equals(candidate.TabId, tabId, StringComparison.Ordinal));
        if (tab is null)
        {
            return;
        }

        ActiveTabId = tabId;
        RemoveMatchingRecent(tab);
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
        RememberRecentTab(tab);
        Reindex();

        if (string.Equals(ActiveTabId, tabId, StringComparison.Ordinal))
        {
            ActiveTabId = _tabs.LastOrDefault()?.TabId;
        }

        EnsureActiveTabIsAwake();
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task CloseOthersAsync(string tabId, CancellationToken cancellationToken = default)
    {
        var remaining = new List<WorkbenchTabState>();
        foreach (var tab in Tabs)
        {
            if (tab.IsPinned || string.Equals(tab.TabId, tabId, StringComparison.Ordinal))
            {
                remaining.Add(tab);
                continue;
            }

            RememberRecentTab(tab);
        }

        _tabs.Clear();
        _tabs.AddRange(remaining.Select((tab, index) => tab with { Order = index }));
        ActiveTabId = _tabs.FirstOrDefault(tab => string.Equals(tab.TabId, tabId, StringComparison.Ordinal))?.TabId
            ?? _tabs.FirstOrDefault()?.TabId;
        EnsureActiveTabIsAwake();
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task CloseRightAsync(string tabId, CancellationToken cancellationToken = default)
    {
        var ordered = Tabs.ToList();
        var index = ordered.FindIndex(tab => string.Equals(tab.TabId, tabId, StringComparison.Ordinal));
        if (index < 0)
        {
            return;
        }

        for (var current = ordered.Count - 1; current > index; current--)
        {
            var candidate = ordered[current];
            if (candidate.IsPinned)
            {
                continue;
            }

            ordered.RemoveAt(current);
            RememberRecentTab(candidate);
        }

        _tabs.Clear();
        _tabs.AddRange(ordered.Select((tab, orderIndex) => tab with { Order = orderIndex }));
        EnsureActiveTabIsAwake();
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task CloseAllBackgroundAsync(string? keepTabId = null, CancellationToken cancellationToken = default)
    {
        keepTabId ??= ActiveTabId;
        var protectedIds = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(keepTabId))
        {
            protectedIds.Add(keepTabId);
        }

        var remaining = new List<WorkbenchTabState>();
        foreach (var tab in Tabs)
        {
            if (tab.IsPinned || protectedIds.Contains(tab.TabId))
            {
                remaining.Add(tab);
                continue;
            }

            RememberRecentTab(tab);
        }

        _tabs.Clear();
        _tabs.AddRange(remaining.Select((tab, index) => tab with { Order = index }));
        ActiveTabId ??= _tabs.FirstOrDefault()?.TabId;
        EnsureActiveTabIsAwake();
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public async Task ReopenRecentAsync(string tabId, CancellationToken cancellationToken = default)
    {
        var recent = _recentTabs.FirstOrDefault(tab => string.Equals(tab.TabId, tabId, StringComparison.Ordinal));
        if (recent is null)
        {
            return;
        }

        var reopened = NormalizeNewTab(recent with
        {
            ClosedAtUtc = null,
            IsSleeping = false,
            LastActivatedAtUtc = clock.GetUtcNow(),
            Order = _tabs.Count
        });
        var existing = FindOpenTabByIdentity(reopened);

        RemoveMatchingRecent(reopened);
        if (existing is not null)
        {
            ActiveTabId = existing.TabId;
            ReplaceTab(existing with { IsSleeping = false, LastActivatedAtUtc = clock.GetUtcNow() });
        }
        else
        {
            _tabs.Add(reopened);
            ActiveTabId = reopened.TabId;
        }

        EnsureActiveTabIsAwake();
        AutoSleepBackgroundTabs();
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

    public async Task UpdateSnapshotAsync(string tabId, string? snapshotJson, CancellationToken cancellationToken = default)
    {
        var tab = _tabs.FirstOrDefault(candidate => string.Equals(candidate.TabId, tabId, StringComparison.Ordinal));
        if (tab is null)
        {
            return;
        }

        ReplaceTab(tab with { SnapshotJson = snapshotJson });
        await PersistAsync(cancellationToken);
        NotifyStateChanged();
    }

    public WorkbenchTabState? GetActiveTab()
        => _tabs.FirstOrDefault(tab => string.Equals(tab.TabId, ActiveTabId, StringComparison.Ordinal));

    private async Task PersistAsync(CancellationToken cancellationToken)
    {
        var snapshot = new WorkbenchSessionSnapshot(
            SnapshotVersion,
            ActiveTabId,
            Tabs.ToArray(),
            CompatibilityMarker,
            SavedAtUtc: clock.GetUtcNow(),
            RestoreFailures: LastRestoreReport?.Failures,
            RecentTabs: _recentTabs.ToArray());

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

    private static bool TryNormalizeRestoredTab(WorkbenchTabState tab, out WorkbenchTabState normalized, out WorkbenchRestoreFailure? failure)
    {
        if (string.IsNullOrWhiteSpace(tab.TabId) || string.IsNullOrWhiteSpace(tab.Route))
        {
            normalized = tab;
            failure = new WorkbenchRestoreFailure(tab.TabId, tab.Title, "Tab route or id was missing.");
            return false;
        }

        var route = NormalizeRoute(tab.Route);
        if (IsRetiredModuleRoute(route))
        {
            normalized = tab with { Route = route };
            failure = new WorkbenchRestoreFailure(tab.TabId, tab.Title, $"Tab route '{route}' belongs to a retired module.");
            return false;
        }

        normalized = NormalizeNewTab(tab);
        failure = null;
        return true;
    }

    private static WorkbenchTabState NormalizeNewTab(WorkbenchTabState tab)
    {
        var route = NormalizeRoute(tab.Route);
        var artifactKey = string.IsNullOrWhiteSpace(tab.ArtifactKey)
            ? $"{tab.TabKind}:{route}"
            : tab.ArtifactKey;

        return tab with
        {
            Route = route,
            RestoreKey = tab.RestoreKey ?? artifactKey,
            ArtifactKey = artifactKey,
            TabGroup = tab.TabGroup ?? ResolveTabGroup(tab.TabKind)
        };
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
            _tabs[index] = NormalizeNewTab(updatedTab);
        }
    }

    private void RememberRecentTab(WorkbenchTabState tab)
    {
        RemoveMatchingRecent(tab);
        _recentTabs.Insert(0, NormalizeNewTab(tab with
        {
            ClosedAtUtc = clock.GetUtcNow(),
            IsSleeping = false
        }));

        if (_recentTabs.Count > MaxRecentTabs)
        {
            _recentTabs.RemoveRange(MaxRecentTabs, _recentTabs.Count - MaxRecentTabs);
        }
    }

    private void RemoveMatchingRecent(WorkbenchTabState tab)
    {
        foreach (var recentTab in _recentTabs.ToList())
        {
            if (IsSameLogicalTab(recentTab, tab))
            {
                _recentTabs.Remove(recentTab);
            }
        }
    }

    private WorkbenchTabState? FindOpenTabByIdentity(WorkbenchTabState candidate)
        => _tabs.FirstOrDefault(tab => IsSameLogicalTab(tab, candidate));

    private void NotifyStateChanged() => Changed?.Invoke();

    private static string NormalizeRoute(string route)
    {
        if (string.IsNullOrWhiteSpace(route))
        {
            return "/";
        }

        return route.StartsWith('/') ? route : $"/{route}";
    }

    private static string ResolveTabIdentity(WorkbenchTabState tab)
    {
        var normalizedRoute = NormalizeRoute(tab.Route);
        var generatedArtifactKey = BuildGeneratedArtifactKey(tab.TabKind, normalizedRoute);
        if (!string.IsNullOrWhiteSpace(tab.ArtifactKey) &&
            !string.Equals(tab.ArtifactKey, generatedArtifactKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"artifact:{tab.ArtifactKey}";
        }

        if (!string.IsNullOrWhiteSpace(tab.RestoreKey) &&
            !string.Equals(tab.RestoreKey, generatedArtifactKey, StringComparison.OrdinalIgnoreCase))
        {
            return $"restore:{tab.RestoreKey}";
        }

        if (tab.ProjectId.HasValue && !string.IsNullOrWhiteSpace(tab.TabKind))
        {
            return $"project:{tab.TabKind}:{tab.ProjectId.Value:N}";
        }

        return $"route:{NormalizeRoutePath(normalizedRoute)}";
    }

    private static bool IsSameLogicalTab(WorkbenchTabState left, WorkbenchTabState right)
        => string.Equals(left.TabId, right.TabId, StringComparison.Ordinal) ||
           HasSameValue(left.ArtifactKey, right.ArtifactKey) ||
           HasSameValue(left.RestoreKey, right.RestoreKey) ||
           HasSameRouteScopedSurface(left, right) ||
           string.Equals(ResolveTabIdentity(left), ResolveTabIdentity(right), StringComparison.OrdinalIgnoreCase);

    private static bool HasSameValue(string? left, string? right)
        => !string.IsNullOrWhiteSpace(left) &&
           !string.IsNullOrWhiteSpace(right) &&
           string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static bool HasSameRouteScopedSurface(WorkbenchTabState left, WorkbenchTabState right)
        => IsRouteScopedTabKind(left.TabKind) &&
           string.Equals(left.TabKind, right.TabKind, StringComparison.OrdinalIgnoreCase) &&
           string.Equals(NormalizeRoutePath(left.Route), NormalizeRoutePath(right.Route), StringComparison.OrdinalIgnoreCase);

    private static bool IsRouteScopedTabKind(string tabKind)
        => tabKind is WorkbenchTabKinds.ProjectStructure or WorkbenchTabKinds.ProjectCalendar or WorkbenchTabKinds.Processes;

    private static string BuildGeneratedArtifactKey(string tabKind, string normalizedRoute)
        => $"{tabKind}:{normalizedRoute}";

    private static string NormalizeRoutePath(string route)
    {
        var normalized = NormalizeRoute(route);
        var queryIndex = normalized.IndexOf('?');
        return queryIndex >= 0 ? normalized[..queryIndex] : normalized;
    }

    private static bool IsRetiredModuleRoute(string route)
    {
        var normalized = NormalizeRoute(route);
        return RetiredModuleRoutes.Any(retiredRoute =>
            string.Equals(normalized, retiredRoute, StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith($"{retiredRoute}?", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith($"{retiredRoute}/", StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveTabGroup(string tabKind) => tabKind switch
    {
        WorkbenchTabKinds.ProjectOverview or WorkbenchTabKinds.ProjectStructure or WorkbenchTabKinds.ProjectCalendar => "Projects",
        WorkbenchTabKinds.PromptWizardSession or WorkbenchTabKinds.PromptDetail => "Prompt Sessions",
        WorkbenchTabKinds.Processes => "Processes",
        WorkbenchTabKinds.ValidationRun => "Validation",
        WorkbenchTabKinds.TestPlan => "Testing",
        WorkbenchTabKinds.Settings => "Settings",
        _ => "Workspace"
    };

    private static string BuildPageTabId(string route)
    {
        var normalized = NormalizeRoute(route).Trim('/');
        return string.IsNullOrWhiteSpace(normalized)
            ? "route:dashboard"
            : $"route:{normalized.Replace('/', ':').Replace('?', ':').Replace('&', ':')}";
    }

    private static string BuildArtifactTabId(string tabKind, Guid? artifactId, string route)
    {
        if (artifactId.HasValue)
        {
            return $"{tabKind}:{artifactId.Value:N}";
        }

        return $"{tabKind}:{NormalizeRoute(route).Trim('/').Replace('/', ':').Replace('?', ':').Replace('&', ':')}";
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
                .ToArray(),
            RecentTabs = snapshot.RecentTabs?
                .Select(tab => tab with { })
                .ToArray()
        };

        return ValueTask.CompletedTask;
    }
}


