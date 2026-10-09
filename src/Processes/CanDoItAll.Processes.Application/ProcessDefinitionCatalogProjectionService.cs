using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Processes.Abstractions;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessDefinitionFeedDefaultsCommand(
    ProcessWorkspaceShellScope Scope);

public sealed class ProcessDefinitionCatalogProjectionService
{
    private const int DefaultTake = 50;
    private const int MaximumTake = 200;
    private readonly ProcessTemplatePackLoader templatePackLoader;
    private readonly IProcessProjectionClock clock;
    private readonly ProcessAuthoringWorkspace? authoring;
    private readonly Lazy<IReadOnlyList<ProcessDefinitionCatalogItemProjection>> catalogItems;

    public ProcessDefinitionCatalogProjectionService(IProcessProjectionClock clock)
        : this(new ProcessTemplatePackLoader(), clock)
    {
    }

    public ProcessDefinitionCatalogProjectionService(
        ProcessTemplatePackLoader templatePackLoader,
        IProcessProjectionClock clock,
        ProcessAuthoringWorkspace? authoring = null)
    {
        this.templatePackLoader = templatePackLoader ?? throw new ArgumentNullException(nameof(templatePackLoader));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.authoring = authoring;
        catalogItems = new Lazy<IReadOnlyList<ProcessDefinitionCatalogItemProjection>>(
            LoadCatalogItems,
            LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public static ProcessDefinitionId CreateDefinitionId(ProcessDefinitionCatalogItemKey key)
        => ProcessTemplateKernelBuilder.CreateDefinitionId(key.Value);

    public string ResolveDefinitionKey(ProcessDefinitionId definitionId)
    {
        return catalogItems.Value
            .FirstOrDefault(item => CreateDefinitionId(item.Key) == definitionId)
            ?.Key.Value ?? string.Empty;
    }

    public async Task<ProcessDefinitionCatalogProjection> GetCatalogAsync(
        ProcessWorkspaceShellScope scope,
        ProcessDefinitionCatalogQueryProjection query,
        ProcessDefinitionCatalogCommandReceipt? lastCommandReceipt = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScope(scope);
        ArgumentNullException.ThrowIfNull(query);

        var normalizedQuery = NormalizeQuery(query);
        var pack = templatePackLoader.Load();
        var allItems = await CurrentItemsAsync(scope, cancellationToken);
        var normalizedSearchText = normalizedQuery.SearchText ?? string.Empty;
        var scopeFilteredItems = FilterByScope(allItems, normalizedQuery.ScopeFilter)
            .ToArray();
        var filteredItems = FilterItems(scopeFilteredItems, normalizedSearchText)
            .Take(normalizedQuery.Take)
            .ToArray();
        var selectedItem = ResolveSelectedItem(filteredItems, normalizedQuery.SelectedDefinitionKey);
        var selectedKey = selectedItem?.Key ?? normalizedQuery.SelectedDefinitionKey;
        var summary = CreateSummary(pack, filteredItems.Length, normalizedSearchText);

        return new ProcessDefinitionCatalogProjection(
            PublishedDefinitionCount: allItems.Count(item => item.CanLaunch),
            DraftDefinitionCount: allItems.Count(item => item.HasDraft),
            TemplateCompatibilityIssueCount: allItems.Sum(item => item.CompatibilityIssueCount),
            summary,
            normalizedSearchText,
            selectedKey,
            CreateScopeGroups(scope, allItems, normalizedQuery.ScopeFilter),
            filteredItems,
            selectedItem,
            SelectedEditor: null,
            lastCommandReceipt);
    }

    public async Task<IReadOnlyList<ProcessDefinitionCatalogItemProjection>> GetCompleteCatalogItemsAsync(
        ProcessWorkspaceShellScope scope,
        string? searchText = null,
        ProcessDefinitionCatalogScopeKind scopeFilter = ProcessDefinitionCatalogScopeKind.All,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScope(scope);

        var normalizedSearchText = string.IsNullOrWhiteSpace(searchText)
            ? string.Empty
            : searchText.Trim();
        IReadOnlyList<ProcessDefinitionCatalogItemProjection> items = FilterItems(
                FilterByScope(await CurrentItemsAsync(scope, cancellationToken), scopeFilter).ToArray(),
                normalizedSearchText)
            .ToArray();

        return items;
    }

    private async Task<IReadOnlyList<ProcessDefinitionCatalogItemProjection>> CurrentItemsAsync(ProcessWorkspaceShellScope scope, CancellationToken cancellationToken) {
        if (authoring is null) {
            return catalogItems.Value;
        }
        var items = catalogItems.Value.ToDictionary(item => item.Key);
        var authored = await authoring.CatalogAsync(scope, cancellationToken);
        foreach (var row in authored.Where(row => row.Lifecycle != ProcessAuthoringLifecycle.Deleted).OrderBy(row => row.Address.ProjectId != Guid.Empty)) {
            var status = row.Lifecycle switch {
                ProcessAuthoringLifecycle.Published => ProcessDefinitionCatalogItemStatus.Published,
                ProcessAuthoringLifecycle.Archived => ProcessDefinitionCatalogItemStatus.Archived,
                _ => ProcessDefinitionCatalogItemStatus.Draft
            };
            ProcessDefinitionCatalogItemKey key = new(row.Address.DefinitionKey);
            var inherited = items.GetValueOrDefault(key);
            var executable = row.Lifecycle == ProcessAuthoringLifecycle.Archived ? ProcessDefinitionExecutableSource.Unavailable
                : row.PublishedId is not null ? row.Address.ProjectId == Guid.Empty
                    ? ProcessDefinitionExecutableSource.GlobalPublication : ProcessDefinitionExecutableSource.ProjectPublication
                : inherited?.ExecutableSource ?? ProcessDefinitionExecutableSource.Unavailable;
            items[key] = new(key, row.Address.ProjectId == Guid.Empty ? ProcessDefinitionCatalogScopeKind.Global : ProcessDefinitionCatalogScopeKind.Project,
                row.Name, row.Summary, status, row.Criticality, row.OperatingMode, row.UpdatedAtUtc, 0) {
                    PublishedId = row.PublishedId, HasDraft = row.Lifecycle == ProcessAuthoringLifecycle.Draft, ExecutableSource = executable
                };
        }
        return items.Values.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ThenBy(item => item.Key.Value, StringComparer.Ordinal).ToArray();
    }

    private IReadOnlyList<ProcessDefinitionCatalogItemProjection> LoadCatalogItems()
    {
        return templatePackLoader.Load().Definitions
            .Select(CreateCatalogItem)
            .OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Key.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public Task<ProcessDefinitionCatalogCommandReceipt> FeedDefaultDefinitionsAsync(
        ProcessDefinitionFeedDefaultsCommand command,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);
        ValidateScope(command.Scope);

        var pack = templatePackLoader.Load();
        var observedAtUtc = clock.GetUtcNow();
        var affectedCount = pack.Definitions.Count;
        var status = affectedCount == 0
            ? ProcessDefinitionCatalogCommandStatus.NoDefinitionsAvailable
            : ProcessDefinitionCatalogCommandStatus.Accepted;
        var summary = affectedCount == 0
            ? "No default process definitions are available in the template pack."
            : $"{affectedCount} default process definition(s) are available from template pack {pack.Manifest.Version}.";

        return Task.FromResult(new ProcessDefinitionCatalogCommandReceipt(
            Guid.NewGuid(),
            ProcessDefinitionCatalogCommandKind.FeedDefaults,
            status,
            new ProcessDefinitionCatalogRefreshToken($"feed-defaults:{pack.Manifest.Version}:{observedAtUtc:yyyyMMddHHmmss}"),
            affectedCount,
            observedAtUtc,
            summary));
    }

    private static ProcessDefinitionCatalogQueryProjection NormalizeQuery(
        ProcessDefinitionCatalogQueryProjection query)
    {
        var take = query.Take <= 0 ? DefaultTake : Math.Min(query.Take, MaximumTake);
        return new ProcessDefinitionCatalogQueryProjection(
            string.IsNullOrWhiteSpace(query.SearchText) ? string.Empty : query.SearchText.Trim(),
            query.SelectedDefinitionKey,
            query.ScopeFilter,
            take);
    }

    private static ProcessDefinitionCatalogItemProjection CreateCatalogItem(
        ProcessTemplateDefinitionSummary definition)
        => new(
            new ProcessDefinitionCatalogItemKey(definition.Key),
            ProcessDefinitionCatalogScopeKind.Global,
            definition.DisplayName,
            definition.Summary,
            ProcessDefinitionCatalogItemStatus.TemplateDefault,
            definition.Criticality,
            definition.OperatingMode,
            definition.UpdatedAtUtc,
            CompatibilityIssueCount: 0);

    private static IEnumerable<ProcessDefinitionCatalogItemProjection> FilterItems(
        IReadOnlyList<ProcessDefinitionCatalogItemProjection> items,
        string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return items;
        }

        return items.Where(item =>
            item.Key.Value.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            item.Name.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            item.Summary.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            item.Criticality.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            item.OperatingMode.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<ProcessDefinitionCatalogItemProjection> FilterByScope(
        IReadOnlyList<ProcessDefinitionCatalogItemProjection> items,
        ProcessDefinitionCatalogScopeKind scopeFilter)
        => scopeFilter switch
        {
            ProcessDefinitionCatalogScopeKind.All => items,
            ProcessDefinitionCatalogScopeKind.Global => items.Where(item => item.ScopeKind == ProcessDefinitionCatalogScopeKind.Global),
            ProcessDefinitionCatalogScopeKind.Project => items.Where(item => item.ScopeKind == ProcessDefinitionCatalogScopeKind.Project),
            _ => throw new ArgumentOutOfRangeException(nameof(scopeFilter), scopeFilter, "Unknown definition catalog scope filter.")
        };

    private static ProcessDefinitionCatalogItemProjection? ResolveSelectedItem(
        IReadOnlyList<ProcessDefinitionCatalogItemProjection> items,
        ProcessDefinitionCatalogItemKey? selectedDefinitionKey)
    {
        if (items.Count == 0)
        {
            return null;
        }

        if (selectedDefinitionKey is { } selectedKey)
        {
            return items.FirstOrDefault(item => item.Key == selectedKey);
        }

        return items[0];
    }

    private static IReadOnlyList<ProcessDefinitionScopeGroupProjection> CreateScopeGroups(
        ProcessWorkspaceShellScope scope,
        IReadOnlyList<ProcessDefinitionCatalogItemProjection> items,
        ProcessDefinitionCatalogScopeKind scopeFilter)
    {
        var projectLabel = scope.ProjectId.HasValue
            ? $"Project {scope.ProjectId.Value:D}"
            : "Project";

        return
        [
            new(
                ProcessDefinitionCatalogScopeKind.All,
                "All definitions",
                "All definitions visible to this workspace.",
                items.Count,
                scopeFilter == ProcessDefinitionCatalogScopeKind.All),
            new(
                ProcessDefinitionCatalogScopeKind.Global,
                "Global defaults",
                "Global publications and inherited templates visible in this workspace.",
                items.Count(item => item.ScopeKind == ProcessDefinitionCatalogScopeKind.Global),
                scopeFilter == ProcessDefinitionCatalogScopeKind.Global),
            new(
                ProcessDefinitionCatalogScopeKind.Project,
                projectLabel,
                "Authored definitions belonging to this project lifetime.",
                items.Count(item => item.ScopeKind == ProcessDefinitionCatalogScopeKind.Project),
                scopeFilter == ProcessDefinitionCatalogScopeKind.Project)
        ];
    }

    private static string CreateSummary(
        ProcessTemplatePack pack,
        int filteredCount,
        string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return $"{pack.Definitions.Count} default definition(s) loaded from template pack {pack.Manifest.Version}.";
        }

        return $"{filteredCount} definition(s) match '{searchText}' in template pack {pack.Manifest.Version}.";
    }

    private static void ValidateScope(ProcessWorkspaceShellScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Kind == ProcessWorkspaceScopeKind.Project && scope.ProjectId is null)
        {
            throw new ArgumentException("Project-scoped definition catalog query requires a project id.", nameof(scope));
        }

        if (scope.Kind == ProcessWorkspaceScopeKind.Global && scope.ProjectId is not null)
        {
            throw new ArgumentException("Global definition catalog query cannot carry a project id.", nameof(scope));
        }
    }
}
