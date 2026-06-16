using System.Globalization;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Templates;

namespace CanDoItAll.Processes.Application;

public sealed class ProcessTemplateCatalogProjectionService
{
    private const int DefaultTake = 50;
    private const int MaximumTake = 150;
    private const string ProcessItemPrefix = "process";
    private const string RoleItemPrefix = "role";
    private const string ArtifactItemPrefix = "artifact";

    private readonly ProcessTemplatePackLoader templatePackLoader;
    private readonly IProcessProjectionClock clock;
    private readonly Dictionary<ProcessTemplateCatalogStateKey, ProcessTemplateCatalogImportSnapshot> importSnapshots = [];

    public ProcessTemplateCatalogProjectionService(IProcessProjectionClock clock)
        : this(new ProcessTemplatePackLoader(), clock)
    {
    }

    public ProcessTemplateCatalogProjectionService(
        ProcessTemplatePackLoader templatePackLoader,
        IProcessProjectionClock clock)
    {
        this.templatePackLoader = templatePackLoader ?? throw new ArgumentNullException(nameof(templatePackLoader));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public Task<ProcessTemplateCatalogProjection> GetCatalogAsync(
        ProcessWorkspaceShellScope scope,
        ProcessDefinitionCatalogItemKey targetDefinitionKey,
        ProcessTemplateCatalogQueryProjection query,
        ProcessDefinitionStepEditorProjection? stepEditor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ValidateScope(scope);
        ArgumentNullException.ThrowIfNull(query);

        var stateKey = ProcessTemplateCatalogStateKey.From(scope, targetDefinitionKey);
        var snapshot = ResolveSnapshot(stateKey);
        var pack = templatePackLoader.Load();
        return Task.FromResult(CreateProjection(
            pack,
            targetDefinitionKey,
            NormalizeQuery(query),
            stepEditor,
            snapshot,
            lastReceipt: null));
    }

    public Task<ProcessTemplateImportCommandResult> ExecuteCommandAsync(
        ProcessTemplateImportCommand command,
        ProcessDefinitionStepEditorProjection? stepEditor,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command.Scope);
        ArgumentNullException.ThrowIfNull(command.Query);
        ValidateScope(command.Scope);

        var stateKey = ProcessTemplateCatalogStateKey.From(command.Scope, command.TargetDefinitionKey);
        var baseline = ResolveSnapshot(stateKey);
        var pack = templatePackLoader.Load();
        var query = NormalizeQuery(command.Query);
        var expectedVersionToken = command.ExpectedVersionToken;
        var observedAtUtc = clock.GetUtcNow();
        if (expectedVersionToken is not null && expectedVersionToken != CreateVersionToken(command.TargetDefinitionKey, baseline.Revision))
        {
            return Task.FromResult(CreateRejectedResult(
                pack,
                command,
                query,
                stepEditor,
                baseline,
                observedAtUtc,
                "Template import was rejected because the template catalog projection changed before submission."));
        }

        var sourceItems = CreateSourceItems(pack);
        var selectedItem = sourceItems.FirstOrDefault(item => item.Key == command.ItemKey);
        if (selectedItem is null)
        {
            return Task.FromResult(CreateRejectedResult(
                pack,
                command,
                query,
                stepEditor,
                baseline,
                observedAtUtc,
                $"Template item '{command.ItemKey.Value}' is not available in the catalog."));
        }

        var targetSteps = CreateImportTargets(stepEditor);
        var validationMessage = ValidateCommand(command, selectedItem, targetSteps);
        if (validationMessage is not null)
        {
            return Task.FromResult(CreateRejectedResult(
                pack,
                command,
                query,
                stepEditor,
                baseline,
                observedAtUtc,
                validationMessage));
        }

        var imported = baseline.ImportedComponents
            .Where(component => component.ItemKey != selectedItem.Key)
            .Append(new ProcessTemplateImportedComponentProjection(
                selectedItem.Key,
                selectedItem.Kind,
                selectedItem.Title,
                selectedItem.SourceDefinitionKey,
                selectedItem.SourceComponentKey,
                selectedItem.Definition.LibrarySummary.SourceJsonHash,
                command.CommandKind == ProcessTemplateImportCommandKind.ImportArtifact
                    ? command.TargetStepKey
                    : null,
                observedAtUtc))
            .ToArray();
        var stored = new ProcessTemplateCatalogImportSnapshot(baseline.Revision + 1, imported);
        importSnapshots[stateKey] = stored;
        var receipt = new ProcessTemplateImportCommandReceipt(
            Guid.NewGuid(),
            command.CommandKind,
            ProcessTemplateImportCommandStatus.Accepted,
            CreateVersionToken(command.TargetDefinitionKey, stored.Revision),
            observedAtUtc,
            CreateAcceptedSummary(command, selectedItem, targetSteps));
        var projection = CreateProjection(
            pack,
            command.TargetDefinitionKey,
            query with { SelectedItemKey = selectedItem.Key },
            stepEditor,
            stored,
            receipt);

        return Task.FromResult(new ProcessTemplateImportCommandResult(receipt, projection));
    }

    private static string? ValidateCommand(
        ProcessTemplateImportCommand command,
        ProcessTemplateCatalogSourceItem item,
        IReadOnlyList<ProcessTemplateImportTargetStepProjection> targetSteps)
    {
        if (command.CommandKind == ProcessTemplateImportCommandKind.ImportProcess &&
            item.Kind != ProcessTemplateCatalogItemKind.Process)
        {
            return "Only process template items can be imported as a process.";
        }

        if (command.CommandKind == ProcessTemplateImportCommandKind.ImportRole &&
            item.Kind != ProcessTemplateCatalogItemKind.Role)
        {
            return "Only role template items can be imported as a role component.";
        }

        if (command.CommandKind != ProcessTemplateImportCommandKind.ImportArtifact)
        {
            return null;
        }

        if (item.Kind != ProcessTemplateCatalogItemKind.Artifact)
        {
            return "Only artifact template items can be imported as an artifact component.";
        }

        if (command.TargetStepKey is null)
        {
            return "Artifact imports require a target step in the selected definition.";
        }

        return targetSteps.Any(target => target.StepKey == command.TargetStepKey)
            ? null
            : $"Artifact target step '{command.TargetStepKey.Value.Value}' is not available in the selected definition.";
    }

    private static string CreateAcceptedSummary(
        ProcessTemplateImportCommand command,
        ProcessTemplateCatalogSourceItem item,
        IReadOnlyList<ProcessTemplateImportTargetStepProjection> targetSteps)
        => command.CommandKind switch
        {
            ProcessTemplateImportCommandKind.ImportProcess => $"Process template '{item.Title}' imported into the selected definition.",
            ProcessTemplateImportCommandKind.ImportRole => $"Role component '{item.Title}' imported into the selected definition.",
            ProcessTemplateImportCommandKind.ImportArtifact => $"Artifact component '{item.Title}' imported into step '{ResolveTargetStepTitle(command.TargetStepKey, targetSteps)}'.",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command.CommandKind, "Unknown template import command.")
        };

    private static string ResolveTargetStepTitle(
        ProcessDefinitionStepKey? targetStepKey,
        IReadOnlyList<ProcessTemplateImportTargetStepProjection> targetSteps)
        => targetStepKey is null
            ? "unknown"
            : targetSteps.FirstOrDefault(target => target.StepKey == targetStepKey)?.Title ?? targetStepKey.Value.Value;

    private ProcessTemplateImportCommandResult CreateRejectedResult(
        ProcessTemplatePack pack,
        ProcessTemplateImportCommand command,
        ProcessTemplateCatalogQueryProjection query,
        ProcessDefinitionStepEditorProjection? stepEditor,
        ProcessTemplateCatalogImportSnapshot snapshot,
        DateTimeOffset observedAtUtc,
        string summary)
    {
        var receipt = new ProcessTemplateImportCommandReceipt(
            Guid.NewGuid(),
            command.CommandKind,
            ProcessTemplateImportCommandStatus.Rejected,
            CreateVersionToken(command.TargetDefinitionKey, snapshot.Revision),
            observedAtUtc,
            summary);
        var projection = CreateProjection(
            pack,
            command.TargetDefinitionKey,
            query,
            stepEditor,
            snapshot,
            receipt);

        return new ProcessTemplateImportCommandResult(receipt, projection);
    }

    private ProcessTemplateCatalogProjection CreateProjection(
        ProcessTemplatePack pack,
        ProcessDefinitionCatalogItemKey targetDefinitionKey,
        ProcessTemplateCatalogQueryProjection query,
        ProcessDefinitionStepEditorProjection? stepEditor,
        ProcessTemplateCatalogImportSnapshot snapshot,
        ProcessTemplateImportCommandReceipt? lastReceipt)
    {
        var allItems = CreateSourceItems(pack);
        var normalizedSearchText = query.SearchText ?? string.Empty;
        var categoryFilteredItems = FilterByCategory(allItems, query.Category).ToArray();
        var filteredSourceItems = FilterItems(categoryFilteredItems, normalizedSearchText)
            .Take(query.Take)
            .ToArray();
        var selectedSourceItem = ResolveSelectedItem(filteredSourceItems, query.SelectedItemKey);
        var selectedItemKey = selectedSourceItem?.Key ?? query.SelectedItemKey;
        var selectedQuery = query with { SelectedItemKey = selectedItemKey };
        var importedKeys = snapshot.ImportedComponents
            .Select(component => component.ItemKey)
            .ToHashSet();
        var selectedItem = selectedSourceItem is null
            ? null
            : CreateItemProjection(selectedSourceItem, importedKeys, IsSelected: true);
        var importTargets = CreateImportTargets(stepEditor);

        return new ProcessTemplateCatalogProjection(
            targetDefinitionKey,
            CreateVersionToken(targetDefinitionKey, snapshot.Revision),
            selectedQuery,
            CreateSummary(pack, filteredSourceItems.Length, normalizedSearchText),
            pack.Manifest.Version,
            "Template catalog is projected from migrated canonical JSON. Markdown and Mermaid previews are generated from the JSON hash.",
            CreateCategories(allItems, query.Category),
            filteredSourceItems
                .Select(item => CreateItemProjection(item, importedKeys, selectedSourceItem?.Key == item.Key))
                .ToArray(),
            selectedItem,
            selectedSourceItem is null
                ? null
                : CreatePreview(selectedSourceItem, allItems, importedKeys),
            importTargets,
            CreateCommands(selectedSourceItem, importTargets),
            snapshot.ImportedComponents,
            lastReceipt);
    }

    private static ProcessTemplateCatalogQueryProjection NormalizeQuery(
        ProcessTemplateCatalogQueryProjection query)
    {
        var take = query.Take <= 0 ? DefaultTake : Math.Min(query.Take, MaximumTake);
        return new ProcessTemplateCatalogQueryProjection(
            string.IsNullOrWhiteSpace(query.SearchText) ? string.Empty : query.SearchText.Trim(),
            query.Category,
            query.SelectedItemKey,
            query.PreviewTab,
            take);
    }

    private static IReadOnlyList<ProcessTemplateCatalogSourceItem> CreateSourceItems(
        ProcessTemplatePack pack)
    {
        var items = new List<ProcessTemplateCatalogSourceItem>();
        foreach (var definition in pack.Definitions)
        {
            items.Add(new ProcessTemplateCatalogSourceItem(
                new ProcessTemplateCatalogItemKey($"{ProcessItemPrefix}:{definition.Key}"),
                ProcessTemplateCatalogItemKind.Process,
                definition.DisplayName,
                definition.Summary,
                definition.Key,
                definition.Key,
                definition,
                Role: null,
                Artifact: null,
                Step: null));

            foreach (var role in definition.RoleAuthoringDefaults.Roles)
            {
                items.Add(new ProcessTemplateCatalogSourceItem(
                    new ProcessTemplateCatalogItemKey($"{RoleItemPrefix}:{definition.Key}:{role.Key}"),
                    ProcessTemplateCatalogItemKind.Role,
                    role.DisplayName,
                    role.Summary,
                    definition.Key,
                    role.Key,
                    definition,
                    role,
                    Artifact: null,
                    Step: null));
            }

            foreach (var step in definition.StepAuthoringDefaults.Steps)
            {
                foreach (var artifact in step.ArtifactExpectations)
                {
                    items.Add(new ProcessTemplateCatalogSourceItem(
                        new ProcessTemplateCatalogItemKey($"{ArtifactItemPrefix}:{definition.Key}:{step.Key}:{artifact.Key}"),
                        ProcessTemplateCatalogItemKind.Artifact,
                        artifact.Title,
                        artifact.ValidationRequirementSummary,
                        definition.Key,
                        artifact.Key,
                        definition,
                        Role: null,
                        artifact,
                        step));
                }
            }
        }

        return items
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Title, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(item => item.Key.Value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IEnumerable<ProcessTemplateCatalogSourceItem> FilterByCategory(
        IReadOnlyList<ProcessTemplateCatalogSourceItem> items,
        ProcessTemplateCatalogCategoryKind category)
        => category switch
        {
            ProcessTemplateCatalogCategoryKind.All => items,
            ProcessTemplateCatalogCategoryKind.Processes => items.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Process),
            ProcessTemplateCatalogCategoryKind.Roles => items.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Role),
            ProcessTemplateCatalogCategoryKind.Artifacts => items.Where(item => item.Kind == ProcessTemplateCatalogItemKind.Artifact),
            _ => throw new ArgumentOutOfRangeException(nameof(category), category, "Unknown template catalog category.")
        };

    private static IEnumerable<ProcessTemplateCatalogSourceItem> FilterItems(
        IReadOnlyList<ProcessTemplateCatalogSourceItem> items,
        string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return items;
        }

        return items.Where(item =>
            item.Key.Value.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            item.Title.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
            item.Summary.Contains(searchText, StringComparison.CurrentCultureIgnoreCase) ||
            item.SourceDefinitionKey.Contains(searchText, StringComparison.OrdinalIgnoreCase) ||
            item.SourceComponentKey.Contains(searchText, StringComparison.OrdinalIgnoreCase));
    }

    private static ProcessTemplateCatalogSourceItem? ResolveSelectedItem(
        IReadOnlyList<ProcessTemplateCatalogSourceItem> items,
        ProcessTemplateCatalogItemKey? selectedItemKey)
    {
        if (items.Count == 0)
        {
            return null;
        }

        if (selectedItemKey is { } key)
        {
            var selected = items.FirstOrDefault(item => item.Key == key);
            if (selected is not null)
            {
                return selected;
            }
        }

        return items[0];
    }

    private static IReadOnlyList<ProcessTemplateCatalogCategoryProjection> CreateCategories(
        IReadOnlyList<ProcessTemplateCatalogSourceItem> allItems,
        ProcessTemplateCatalogCategoryKind selectedCategory)
        =>
        [
            new(
                ProcessTemplateCatalogCategoryKind.All,
                "All",
                "All process, role, and artifact templates.",
                allItems.Count,
                selectedCategory == ProcessTemplateCatalogCategoryKind.All),
            new(
                ProcessTemplateCatalogCategoryKind.Processes,
                "Processes",
                "Process definition templates.",
                allItems.Count(item => item.Kind == ProcessTemplateCatalogItemKind.Process),
                selectedCategory == ProcessTemplateCatalogCategoryKind.Processes),
            new(
                ProcessTemplateCatalogCategoryKind.Roles,
                "Roles",
                "Reusable role components declared by process templates.",
                allItems.Count(item => item.Kind == ProcessTemplateCatalogItemKind.Role),
                selectedCategory == ProcessTemplateCatalogCategoryKind.Roles),
            new(
                ProcessTemplateCatalogCategoryKind.Artifacts,
                "Artifacts",
                "Artifact expectations that can be attached to a selected step.",
                allItems.Count(item => item.Kind == ProcessTemplateCatalogItemKind.Artifact),
                selectedCategory == ProcessTemplateCatalogCategoryKind.Artifacts)
        ];

    private static ProcessTemplateCatalogItemProjection CreateItemProjection(
        ProcessTemplateCatalogSourceItem item,
        ISet<ProcessTemplateCatalogItemKey> importedKeys,
        bool IsSelected)
        => new(
            item.Key,
            item.Kind,
            item.Title,
            string.IsNullOrWhiteSpace(item.Summary) ? "No summary is declared for this template component." : item.Summary,
            item.SourceDefinitionKey,
            item.SourceComponentKey,
            ResolveCategoryLabel(item.Kind),
            CreateFacts(item, importedKeys),
            IsSelected);

    private static IReadOnlyList<ProcessTemplateCatalogFactProjection> CreateFacts(
        ProcessTemplateCatalogSourceItem item,
        ISet<ProcessTemplateCatalogItemKey> importedKeys)
    {
        var facts = new List<ProcessTemplateCatalogFactProjection>
        {
            new("Source", item.SourceDefinitionKey),
            new("Hash", item.Definition.LibrarySummary.SourceJsonHash[..Math.Min(18, item.Definition.LibrarySummary.SourceJsonHash.Length)])
        };
        if (importedKeys.Contains(item.Key))
        {
            facts.Add(new ProcessTemplateCatalogFactProjection("Import", "Imported"));
        }

        if (item.Role is not null)
        {
            facts.Add(new ProcessTemplateCatalogFactProjection("Executor", item.Role.PreferredExecutorKind));
        }

        if (item.Artifact is not null)
        {
            facts.Add(new ProcessTemplateCatalogFactProjection("Artifact", item.Artifact.ArtifactKind));
        }

        return facts;
    }

    private static ProcessTemplateCatalogPreviewProjection CreatePreview(
        ProcessTemplateCatalogSourceItem selectedItem,
        IReadOnlyList<ProcessTemplateCatalogSourceItem> allItems,
        ISet<ProcessTemplateCatalogItemKey> importedKeys)
    {
        var definition = selectedItem.Definition;
        var relatedComponents = allItems
            .Where(item => item.SourceDefinitionKey == definition.Key && item.Kind != ProcessTemplateCatalogItemKind.Process)
            .Select(item => new ProcessTemplateRelatedComponentProjection(
                item.Key,
                item.Kind,
                item.Title,
                item.Summary,
                item.SourceDefinitionKey,
                item.SourceComponentKey,
                importedKeys.Contains(item.Key)))
            .ToArray();

        return new ProcessTemplateCatalogPreviewProjection(
            selectedItem.Key,
            selectedItem.Kind,
            selectedItem.Title,
            selectedItem.Summary,
            definition.LibrarySummary.SourceJsonRelativePath,
            definition.LibrarySummary.SourceJsonHash,
            "Overview, Markdown, diagram, and structure tabs are generated projections from canonical JSON; the JSON tab is the source-backed payload.",
            definition.LibrarySummary.GeneratedMarkdown,
            definition.LibrarySummary.GeneratedMermaid,
            definition.LibrarySummary.CanonicalJson,
            definition.LibrarySummary.StructureNodes
                .Select(node => new ProcessTemplateStructureNodeProjection(
                    node.NodeKey,
                    node.ParentNodeKey,
                    ParseStructureKind(node.Kind),
                    node.Title,
                    node.Summary,
                    node.Depth))
                .ToArray(),
            relatedComponents);
    }

    private static ProcessTemplateStructureNodeKind ParseStructureKind(string kind)
        => Enum.TryParse<ProcessTemplateStructureNodeKind>(kind, ignoreCase: true, out var parsed)
            ? parsed
            : ProcessTemplateStructureNodeKind.Section;

    private static IReadOnlyList<ProcessTemplateImportTargetStepProjection> CreateImportTargets(
        ProcessDefinitionStepEditorProjection? stepEditor)
    {
        if (stepEditor is null)
        {
            return [];
        }

        return stepEditor.Steps
            .OrderBy(step => step.Order)
            .ThenBy(step => step.Title, StringComparer.CurrentCultureIgnoreCase)
            .Select(step => new ProcessTemplateImportTargetStepProjection(
                step.StepKey,
                step.Title,
                step.Subtitle,
                step.IsSelected))
            .ToArray();
    }

    private static IReadOnlyList<ProcessTemplateImportCommandProjection> CreateCommands(
        ProcessTemplateCatalogSourceItem? selectedItem,
        IReadOnlyList<ProcessTemplateImportTargetStepProjection> targetSteps)
        =>
        [
            new(
                ProcessTemplateImportCommandKind.ImportProcess,
                "Import process",
                "account_tree",
                selectedItem?.Kind == ProcessTemplateCatalogItemKind.Process,
                selectedItem is null
                    ? "Select a template item first."
                    : selectedItem.Kind == ProcessTemplateCatalogItemKind.Process
                        ? null
                        : "Select a process template item."),
            new(
                ProcessTemplateImportCommandKind.ImportRole,
                "Import role",
                "badge",
                selectedItem?.Kind == ProcessTemplateCatalogItemKind.Role,
                selectedItem is null
                    ? "Select a template item first."
                    : selectedItem.Kind == ProcessTemplateCatalogItemKind.Role
                        ? null
                        : "Select a role component."),
            new(
                ProcessTemplateImportCommandKind.ImportArtifact,
                "Import artifact",
                "inventory_2",
                selectedItem?.Kind == ProcessTemplateCatalogItemKind.Artifact && targetSteps.Count > 0,
                targetSteps.Count == 0
                    ? "Artifact import requires at least one target step."
                    : selectedItem?.Kind == ProcessTemplateCatalogItemKind.Artifact
                        ? null
                        : "Select an artifact component.")
        ];

    private static string ResolveCategoryLabel(ProcessTemplateCatalogItemKind kind)
        => kind switch
        {
            ProcessTemplateCatalogItemKind.Process => "Process",
            ProcessTemplateCatalogItemKind.Role => "Role",
            ProcessTemplateCatalogItemKind.Artifact => "Artifact",
            _ => "Template"
        };

    private static string CreateSummary(
        ProcessTemplatePack pack,
        int filteredCount,
        string searchText)
    {
        if (string.IsNullOrWhiteSpace(searchText))
        {
            return $"{filteredCount.ToString(CultureInfo.InvariantCulture)} template catalog item(s) from pack {pack.Manifest.Version}.";
        }

        return $"{filteredCount.ToString(CultureInfo.InvariantCulture)} template catalog item(s) match '{searchText}' in pack {pack.Manifest.Version}.";
    }

    private static ProcessTemplateCatalogVersionToken CreateVersionToken(
        ProcessDefinitionCatalogItemKey targetDefinitionKey,
        int revision)
        => new($"templates:{targetDefinitionKey.Value}:{revision}");

    private static void ValidateScope(ProcessWorkspaceShellScope scope)
    {
        ArgumentNullException.ThrowIfNull(scope);
        if (scope.Kind == ProcessWorkspaceScopeKind.Project && scope.ProjectId is null)
        {
            throw new ArgumentException("Project-scoped template catalog query requires a project id.", nameof(scope));
        }

        if (scope.Kind == ProcessWorkspaceScopeKind.Global && scope.ProjectId is not null)
        {
            throw new ArgumentException("Global template catalog query cannot carry a project id.", nameof(scope));
        }
    }

    private static ProcessTemplateCatalogImportSnapshot CreateEmptySnapshot()
        => new(Revision: 0, ImportedComponents: []);

    private ProcessTemplateCatalogImportSnapshot ResolveSnapshot(
        ProcessTemplateCatalogStateKey stateKey)
        => importSnapshots.TryGetValue(stateKey, out var snapshot)
            ? snapshot
            : CreateEmptySnapshot();

    private readonly record struct ProcessTemplateCatalogStateKey(
        string ScopeKey,
        string DefinitionKey)
    {
        public static ProcessTemplateCatalogStateKey From(
            ProcessWorkspaceShellScope scope,
            ProcessDefinitionCatalogItemKey definitionKey)
            => new(
                scope.Kind == ProcessWorkspaceScopeKind.Project
                    ? $"project:{scope.ProjectId:N}"
                    : "global",
                definitionKey.Value);
    }

    private sealed record ProcessTemplateCatalogImportSnapshot(
        int Revision,
        IReadOnlyList<ProcessTemplateImportedComponentProjection> ImportedComponents);

    private sealed record ProcessTemplateCatalogSourceItem(
        ProcessTemplateCatalogItemKey Key,
        ProcessTemplateCatalogItemKind Kind,
        string Title,
        string Summary,
        string SourceDefinitionKey,
        string SourceComponentKey,
        ProcessTemplateDefinitionSummary Definition,
        ProcessTemplateDefinitionRoleSummary? Role,
        ProcessTemplateDefinitionStepArtifactExpectationSummary? Artifact,
        ProcessTemplateDefinitionStepAuthoringSummary? Step);
}
