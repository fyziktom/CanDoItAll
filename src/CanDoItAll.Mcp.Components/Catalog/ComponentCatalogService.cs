using System.Reflection;
using System.Text.RegularExpressions;
using System.Globalization;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.Charts;
using CanDoItAll.Components.Mermaid;
using CanDoItAll.Components.Sandbox;
using CanDoItAll.Mcp.Components.Configuration;
using CanDoItAll.Mcp.Core.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Components.Catalog;

public sealed partial class ComponentCatalogService
{
    private const string ComponentsRepositoryRelativeRoot = @"..\CanDoItAll.Components";

    private readonly Lazy<ComponentCatalogIndex> index;
    private readonly McpServerOptions options;

    public ComponentCatalogService(IOptions<McpServerOptions> optionsAccessor)
    {
        options = optionsAccessor.Value;
        index = new Lazy<ComponentCatalogIndex>(BuildIndex, LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public ComponentsSearchData Search(string? query, string? library = null, string? group = null, int limit = 10)
    {
        var catalog = index.Value;
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var groupLookup = catalog.Groups.ToDictionary(item => item.Key, StringComparer.OrdinalIgnoreCase);
        var normalizedLibrary = library?.Trim();
        var normalizedGroup = group?.Trim();

        var componentHits = catalog.Components
            .Where(component => normalizedLibrary is null || string.Equals(component.Library, normalizedLibrary, StringComparison.OrdinalIgnoreCase))
            .Where(component => normalizedGroup is null || component.GroupKeys.Contains(normalizedGroup, StringComparer.OrdinalIgnoreCase))
            .Select(component =>
            {
                var score = ScoreComponent(component, normalizedQuery, groupLookup);
                return new
                {
                    Component = component,
                    Score = score.Score,
                    score.MatchedParameters
                };
            })
            .Where(result => string.IsNullOrWhiteSpace(normalizedQuery) || result.Score > 0)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Component.Name, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 50))
            .Select(result => new ComponentSearchHit(
                result.Component.Name,
                result.Component.Library,
                result.Component.Summary,
                result.Score,
                result.Component.GroupKeys
                    .Select(key => groupLookup.TryGetValue(key, out var groupDocument) ? groupDocument.Title : key)
                    .ToArray(),
                result.MatchedParameters))
            .ToArray();

        var exampleHits = catalog.Examples
            .Where(example => normalizedGroup is null || string.Equals(example.GroupKey, normalizedGroup, StringComparison.OrdinalIgnoreCase))
            .Where(example => normalizedLibrary is null || ExampleMatchesLibrary(example, normalizedLibrary, catalog.Components))
            .Where(example => string.IsNullOrWhiteSpace(normalizedQuery) || MatchesExample(example, normalizedQuery))
            .OrderBy(example => example.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 50))
            .ToArray();

        var groupHits = catalog.Groups
            .Where(item => string.IsNullOrWhiteSpace(normalizedQuery) || MatchesGroup(item, normalizedQuery))
            .OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase)
            .Take(Math.Clamp(limit, 1, 50))
            .ToArray();

        return new ComponentsSearchData(normalizedQuery, componentHits, exampleHits, groupHits);
    }

    public ComponentDocument GetComponent(string component)
    {
        return ResolveComponent(component);
    }

    public ComponentExamplesData GetExamples(string component)
    {
        var resolvedComponent = ResolveComponent(component);
        var examples = index.Value.Examples
            .Where(example => example.ComponentNames.Contains(resolvedComponent.Name, StringComparer.OrdinalIgnoreCase))
            .OrderBy(example => example.GroupTitle, StringComparer.OrdinalIgnoreCase)
            .ThenBy(example => example.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new ComponentExamplesData(resolvedComponent.Name, examples);
    }

    public ComponentUsageExamplesData GetUsageExamples(string component, int limit = 10)
    {
        var resolvedComponent = ResolveComponent(component);
        var usageExamples = index.Value.UsageExamplesByComponent.TryGetValue(resolvedComponent.Name, out var matches)
            ? matches
            : [];

        return new ComponentUsageExamplesData(
            resolvedComponent.Name,
            usageExamples.Count,
            usageExamples
                .Take(Math.Clamp(limit, 1, 50))
                .ToArray());
    }

    public IReadOnlyList<ComponentGroupDocument> GetGroups()
    {
        return index.Value.Groups
            .OrderBy(group => group.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public ComponentCssTokensData GetCssTokens(string component)
    {
        var resolvedComponent = ResolveComponent(component);
        var stylesheets = resolvedComponent.Library switch
        {
            "CanvasLib" => CanvasLibStylesheets,
            "Charts" => ["_content/Blazor-ApexCharts/css/apexcharts.css"],
            "Mermaid" => ["_content/CanDoItAll.Components.Mermaid/js/mermaidDiagram.js"],
            _ => new[] { "_content/CanDoItAll.Components.BaseLib/css/output.css" }
        };
        var sourceFiles = ResolveCssSourceFiles(resolvedComponent);

        return new ComponentCssTokensData(resolvedComponent.Name, resolvedComponent.Library, stylesheets, sourceFiles, resolvedComponent.CssNotes);
    }

    private IReadOnlyList<string> ResolveCssSourceFiles(ComponentDocument component)
    {
        return ResolveCssSourceFileHints(component.Library, component.Name, component.SourcePath)
            .Select(path => Path.GetFullPath(Path.Combine(options.Server.WorkspaceRoot, path)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(File.Exists)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public CanvasContractsData GetCanvasContracts(string? query)
    {
        var normalizedQuery = query?.Trim() ?? string.Empty;
        var matches = index.Value.CanvasContracts
            .Where(contract => string.IsNullOrWhiteSpace(normalizedQuery) || MatchesCanvasContract(contract, normalizedQuery))
            .OrderBy(contract => contract.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (matches.Length == 0)
        {
            throw new ToolInvocationException("ContractNotFound", $"No canvas contract matched '{normalizedQuery}'.");
        }

        return new CanvasContractsData(normalizedQuery, matches);
    }

    public ComponentCatalogIndex GetIndex()
    {
        return index.Value;
    }

    private ComponentDocument ResolveComponent(string component)
    {
        if (string.IsNullOrWhiteSpace(component))
        {
            throw new ToolInvocationException("ValidationFailed", "A component name is required.");
        }

        var matches = index.Value.Components
            .Where(candidate =>
                string.Equals(candidate.Name, component, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(candidate.FullName, component, StringComparison.OrdinalIgnoreCase) ||
                candidate.Name.Contains(component, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (matches.Length == 0)
        {
            throw new ToolInvocationException("ComponentNotFound", $"No shared component matched '{component}'.");
        }

        var exact = matches.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, component, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(candidate.FullName, component, StringComparison.OrdinalIgnoreCase));
        if (exact is not null)
        {
            return exact;
        }

        if (matches.Length == 1)
        {
            return matches[0];
        }

        throw new ToolInvocationException(
            "AmbiguousComponent",
            $"Component query '{component}' matched multiple shared components.",
            matches.Select(candidate => candidate.Name).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private ComponentCatalogIndex BuildIndex()
    {
        var workspaceRoot = Path.GetFullPath(options.Server.WorkspaceRoot);
        var baseLibRoot = Path.GetFullPath(Path.Combine(workspaceRoot, options.Catalog.BaseLibRoot));
        var canvasLibRoot = Path.GetFullPath(Path.Combine(workspaceRoot, options.Catalog.CanvasLibRoot));
        var chartsRoot = Path.GetFullPath(Path.Combine(workspaceRoot, options.Catalog.ChartsRoot));
        var mermaidRoot = Path.GetFullPath(Path.Combine(workspaceRoot, options.Catalog.MermaidRoot));
        var sandboxRoot = Path.GetFullPath(Path.Combine(workspaceRoot, options.Catalog.SandboxRoot));

        var groups = BuildGroups();
        var groupLookup = groups.ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var examples = BuildExamples(groupLookup);

        var libraries = new[]
        {
            new LibraryDescriptor("BaseLib", typeof(Button).Assembly, baseLibRoot, "CanDoItAll.Components.BaseLib"),
            new LibraryDescriptor("CanvasLib", typeof(CanvasWorkbench).Assembly, canvasLibRoot, "CanDoItAll.Components.CanvasLib"),
            new LibraryDescriptor("Charts", typeof(CdaChart).Assembly, chartsRoot, "CanDoItAll.Components.Charts"),
            new LibraryDescriptor("Mermaid", typeof(MermaidDiagram).Assembly, mermaidRoot, "CanDoItAll.Components.Mermaid")
        };

        var componentTypes = libraries
            .SelectMany(library => DiscoverComponentTypes(library).Select(type => (Library: library, Type: type)))
            .OrderBy(item => GetComponentName(item.Type), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var componentNames = componentTypes
            .Select(item => GetComponentName(item.Type))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usageExamplesByComponent = BuildConsumerUsageExamples(workspaceRoot, sandboxRoot, componentNames);

        var components = componentTypes
            .Select(item => BuildComponentDocument(item.Library, item.Type, examples, groupLookup, componentNames, usageExamplesByComponent))
            .ToArray();

        var canvasContracts = BuildCanvasContracts();

        return new ComponentCatalogIndex(components, examples, groups, canvasContracts, usageExamplesByComponent);
    }

    private static IReadOnlyList<ComponentGroupDocument> BuildGroups()
    {
        var examplesByGroup = SandboxCatalogRegistry.Examples
            .GroupBy(example => GetGroupKeyFromRoute(SandboxCatalogRegistry.GetGroup(example.GroupKey).Route), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

        return SandboxCatalogRegistry.Groups
            .Select(group =>
            {
                var key = GetGroupKeyFromRoute(group.Route);
                return new ComponentGroupDocument(
                    key,
                    group.Title,
                    group.Route,
                    group.Summary,
                    group.FocusAreas,
                    group.ProofNotes,
                    examplesByGroup.TryGetValue(key, out var exampleCount) ? exampleCount : 0);
            })
            .ToArray();
    }

    private static IReadOnlyList<ComponentExampleDocument> BuildExamples(IReadOnlyDictionary<string, ComponentGroupDocument> groupLookup)
    {
        return SandboxCatalogRegistry.Examples
            .Select(example =>
            {
                var group = SandboxCatalogRegistry.GetGroup(example.GroupKey);
                var groupKey = GetGroupKeyFromRoute(group.Route);
                return new ComponentExampleDocument(
                    example.Id,
                    example.Title,
                    example.Route,
                    groupKey,
                    groupLookup[groupKey].Title,
                    example.Scenario.ToLabel(),
                    example.Summary,
                    example.Tags,
                    example.ComponentNames);
            })
            .ToArray();
    }

    private static IEnumerable<Type> DiscoverComponentTypes(LibraryDescriptor library)
    {
        return library.Assembly
            .GetExportedTypes()
            .Where(type =>
                typeof(IComponent).IsAssignableFrom(type) &&
                type.IsClass &&
                !type.IsAbstract &&
                string.Equals(type.Namespace, library.ComponentNamespace, StringComparison.Ordinal));
    }

    private static ComponentDocument BuildComponentDocument(
        LibraryDescriptor library,
        Type type,
        IReadOnlyList<ComponentExampleDocument> examples,
        IReadOnlyDictionary<string, ComponentGroupDocument> groupLookup,
        IReadOnlySet<string> componentNames,
        IReadOnlyDictionary<string, IReadOnlyList<ComponentUsageExampleDocument>> usageExamplesByComponent)
    {
        var componentName = GetComponentName(type);
        var sourcePath = ResolveSourcePath(library.SourceRoot, componentName);
        var sourceText = File.Exists(sourcePath) ? File.ReadAllText(sourcePath) : string.Empty;

        var relatedExamples = examples
            .Where(example => example.ComponentNames.Contains(componentName, StringComparer.OrdinalIgnoreCase))
            .ToArray();

        var groupKeys = relatedExamples
            .Select(example => example.GroupKey)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var tags = BuildTags(componentName, relatedExamples, groupKeys);
        var dependencyNames = DiscoverDependencies(sourceText, componentNames, componentName);
        var (parameters, events) = BuildParameterDocuments(componentName, type);
        var cssNotes = BuildCssNotes(componentName, library.Name, sourcePath);
        var guidance = BuildGuidance(componentName, library.Name, sourcePath);
        var usageExamples = usageExamplesByComponent.TryGetValue(componentName, out var matches)
            ? matches
            : [];

        return new ComponentDocument(
            componentName,
            type.FullName ?? componentName,
            type.Namespace ?? string.Empty,
            library.Name,
            BuildSummary(componentName, library.Name, sourcePath, groupKeys, groupLookup, relatedExamples),
            sourcePath,
            tags,
            groupKeys,
            dependencyNames,
            parameters,
            events,
            cssNotes,
            guidance,
            usageExamples.Count,
            usageExamples.Take(5).ToArray());
    }

    private static IReadOnlyList<CanvasContractDocument> BuildCanvasContracts()
    {
        var assembly = typeof(CanvasWorkbenchSurface).Assembly;
        return assembly
            .GetExportedTypes()
            .Where(type =>
                type.IsClass &&
                !type.IsAbstract &&
                !typeof(IComponent).IsAssignableFrom(type) &&
                (type.Name.StartsWith("CanvasWorkbench", StringComparison.Ordinal) ||
                 type.Name.StartsWith("CanvasCalendar", StringComparison.Ordinal)) &&
                !type.Name.EndsWith("Snapshot", StringComparison.Ordinal) &&
                !type.Name.EndsWith("Factory", StringComparison.Ordinal))
            .OrderBy(type => type.Name, StringComparer.OrdinalIgnoreCase)
            .Select(type => new CanvasContractDocument(
                type.Name,
                type.FullName ?? type.Name,
                ResolveCanvasContractKind(type.Name),
                BuildCanvasContractSummary(type.Name),
                type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
                    .Where(property => property.GetMethod is not null)
                    .Select(property => new CanvasContractPropertyDocument(property.Name, FormatTypeName(property.PropertyType)))
                    .ToArray()))
            .ToArray();
    }

    private static (IReadOnlyList<ComponentParameterDocument> Parameters, IReadOnlyList<ComponentEventDocument> Events) BuildParameterDocuments(string componentName, Type type)
    {
        var defaultInstance = TryCreateDefaultInstance(type);
        var parameterProperties = type
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(property =>
                property.GetCustomAttribute<ParameterAttribute>() is not null ||
                property.GetCustomAttribute<CascadingParameterAttribute>() is not null)
            .OrderBy(property => property.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var parameters = parameterProperties
            .Where(property => !IsEventCallback(property.PropertyType))
            .Select(property => new ComponentParameterDocument(
                property.Name,
                FormatTypeName(property.PropertyType),
                property.GetCustomAttribute<EditorRequiredAttribute>() is not null,
                property.GetCustomAttribute<CascadingParameterAttribute>() is not null,
                IsChildContent(property.PropertyType),
                BuildParameterSummary(componentName, property.Name),
                ResolveDefaultValue(property, defaultInstance),
                ResolveAllowedValues(property.PropertyType)))
            .ToArray();

        var events = parameterProperties
            .Where(property => IsEventCallback(property.PropertyType))
            .Select(property => new ComponentEventDocument(property.Name, FormatTypeName(property.PropertyType)))
            .ToArray();

        return (parameters, events);
    }

    private static IReadOnlyList<string> DiscoverDependencies(string sourceText, IReadOnlySet<string> componentNames, string componentName)
    {
        var dependencies = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (!string.IsNullOrWhiteSpace(sourceText))
        {
            foreach (var dependency in ComponentReferenceRegex
                         .Matches(sourceText)
                         .Select(match => match.Groups["name"].Value)
                         .Where(name => componentNames.Contains(name) && !string.Equals(name, componentName, StringComparison.OrdinalIgnoreCase)))
            {
                dependencies.Add(dependency);
            }
        }

        if (AdditionalDependenciesByComponent.TryGetValue(componentName, out var additionalDependencies))
        {
            foreach (var dependency in additionalDependencies.Where(componentNames.Contains))
            {
                dependencies.Add(dependency);
            }
        }

        return dependencies
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildSummary(
        string componentName,
        string library,
        string sourcePath,
        IReadOnlyList<string> groupKeys,
        IReadOnlyDictionary<string, ComponentGroupDocument> groupLookup,
        IReadOnlyList<ComponentExampleDocument> examples)
    {
        if (SummaryByComponent.TryGetValue(componentName, out var summary))
        {
            return summary;
        }

        var family = ResolveComponentFamily(library, sourcePath);

        if (string.Equals(library, "BaseLib", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(family))
        {
            var displayName = HumanizeComponentName(componentName);
            return family switch
            {
                "Badges" => $"Shared BaseLib {displayName} component for compact status, counts, and categorical emphasis.",
                "Buttons" => $"Shared BaseLib {displayName} component for primary, secondary, and inline actions.",
                "Cards" => $"Shared BaseLib {displayName} surface for grouped content, summaries, metrics, or action clusters.",
                "DataVisualization" => $"Shared BaseLib {displayName} component for charts, data grids, axes, or quantitative readouts.",
                "Feedback" => $"Shared BaseLib {displayName} component for alerts, loading, empty, notification, or contextual-help states.",
                "Forms" => $"Shared BaseLib {displayName} component for data entry, configuration, and field-level workflows.",
                "Identity" => $"Shared BaseLib {displayName} component for identity, presence, icons, and attribution details.",
                "Layout" => $"Shared BaseLib {displayName} component for responsive structure, spacing rhythm, and workspace composition.",
                "Lists" => $"Shared BaseLib {displayName} component for list, metadata, and selection-driven detail flows.",
                "Modals" => $"Shared BaseLib {displayName} component for modal, dialog, or transient overlay interactions.",
                "Navigation" => $"Shared BaseLib {displayName} component for navigation, orientation, and dense workspace movement.",
                "Storage" => $"Shared BaseLib {displayName} component for storage-oriented summaries, status, and capacity display.",
                "Typography" => $"Shared BaseLib {displayName} component for consistent text hierarchy, rhythm, and emphasis.",
                _ => $"Shared BaseLib {displayName} component for reusable app UI composition."
            };
        }

        if (string.Equals(library, "CanvasLib", StringComparison.OrdinalIgnoreCase) &&
            !string.IsNullOrWhiteSpace(family))
        {
            var displayName = HumanizeComponentName(componentName);
            return family switch
            {
                "Calendar" => $"Shared CanvasLib {displayName} component for calendar-specific runtime, editing, and selection surfaces.",
                "Core" => $"Shared CanvasLib {displayName} component for core workbench runtime infrastructure.",
                "Diagnostics" => $"Shared CanvasLib {displayName} component for diagnostics and runtime debugging overlays.",
                "Graph" => $"Shared CanvasLib {displayName} component for graph composition, interaction, overlays, or primitives.",
                "Shared" => $"Shared CanvasLib {displayName} component for reusable canvas runtime assets and shared support surfaces.",
                "Workbench" => $"Shared CanvasLib {displayName} component for workbench shells, stages, and desktop-first canvas workflows.",
                _ => $"Shared CanvasLib {displayName} component for reusable canvas runtime composition."
            };
        }

        if (string.Equals(library, "Charts", StringComparison.OrdinalIgnoreCase))
        {
            var displayName = HumanizeComponentName(componentName);
            return $"Shared Charts {displayName} component for Apex-backed operational chart rendering behind a CanDoItAll-owned API.";
        }

        if (groupKeys.Count > 0)
        {
            var primaryGroup = groupLookup[groupKeys[0]];
            return $"Shared {library} component aligned with the {primaryGroup.Title} component group. {primaryGroup.Summary}";
        }

        if (componentName.Contains("Calendar", StringComparison.Ordinal))
        {
            return "Shared CanvasLib component for calendar runtime or calendar boundary preview surfaces.";
        }

        if (componentName.Contains("Canvas", StringComparison.Ordinal))
        {
            return "Shared CanvasLib component for the workbench runtime, boundary previews, or canvas-specific documentation surfaces.";
        }

        if (examples.Count > 0)
        {
            return $"Shared {library} component with documented usage coverage through {examples[0].Title}.";
        }

        return $"Shared {library} component for reusable UI composition in the {library} library.";
    }

    private static IReadOnlyList<string> BuildCssNotes(string componentName, string library, string sourcePath)
    {
        var noteSet = new List<string>();

        if (CssNotesByComponent.TryGetValue(componentName, out var notes))
        {
            noteSet.AddRange(notes);
        }
        else if (DefaultCssNotesByLibrary.TryGetValue(library, out var defaultNotes))
        {
            noteSet.AddRange(defaultNotes);
        }

        var sourceFiles = ResolveCssSourceFileHints(library, componentName, sourcePath);
        if (sourceFiles.Count > 0)
        {
            noteSet.Add($"Relevant source styling files: {string.Join(", ", sourceFiles.Select(path => $"`{path}`"))}.");
        }
        else if (string.Equals(library, "BaseLib", StringComparison.OrdinalIgnoreCase))
        {
            noteSet.Add("This component currently relies mostly on inline Tailwind utility classes inside the Razor source, so inspect the component source path when no dedicated Tailwind source file is listed.");
        }

        return noteSet
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static ComponentGuidanceDocument BuildGuidance(string componentName, string library, string sourcePath)
    {
        if (GuidanceByComponent.TryGetValue(componentName, out var guidance))
        {
            return guidance;
        }

        var family = ResolveComponentFamily(library, sourcePath);
        if (!string.IsNullOrWhiteSpace(family) && GuidanceByFamily.TryGetValue(family, out var familyGuidance))
        {
            return familyGuidance;
        }

        return DefaultGuidanceByLibrary.TryGetValue(library, out var defaultGuidance)
            ? defaultGuidance
            : new ComponentGuidanceDocument([], [], []);
    }

    private static IReadOnlyList<string> BuildTags(
        string componentName,
        IReadOnlyList<ComponentExampleDocument> relatedExamples,
        IReadOnlyList<string> groupKeys)
    {
        var tags = relatedExamples
            .SelectMany(example => example.Tags)
            .Concat(groupKeys)
            .ToList();

        if (TagsByComponent.TryGetValue(componentName, out var componentTags))
        {
            tags.AddRange(componentTags);
        }

        return tags
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ComponentUsageExampleDocument>> BuildConsumerUsageExamples(
        string workspaceRoot,
        string sandboxRoot,
        IReadOnlySet<string> componentNames)
    {
        var usageLookup = new Dictionary<string, List<ComponentUsageExampleDocument>>(StringComparer.OrdinalIgnoreCase);

        foreach (var consumerRoot in DiscoverConsumerRoots(workspaceRoot, sandboxRoot))
        {
            foreach (var filePath in Directory.EnumerateFiles(consumerRoot.RootPath, "*.razor", SearchOption.AllDirectories))
            {
                if (IsGeneratedPath(filePath))
                {
                    continue;
                }

                var lines = File.ReadAllLines(filePath);
                var route = ResolveRoute(lines);

                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    var line = lines[lineIndex];
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    foreach (Match match in ComponentReferenceRegex.Matches(line))
                    {
                        var componentName = match.Groups["name"].Value;
                        if (!componentNames.Contains(componentName))
                        {
                            continue;
                        }

                        if (!usageLookup.TryGetValue(componentName, out var examples))
                        {
                            examples = [];
                            usageLookup[componentName] = examples;
                        }

                        examples.Add(new ComponentUsageExampleDocument(
                            consumerRoot.SourceKind,
                            consumerRoot.ProjectName,
                            filePath,
                            lineIndex + 1,
                            TruncateSnippet(line.Trim()),
                            route));
                    }
                }
            }
        }

        return usageLookup.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<ComponentUsageExampleDocument>)pair.Value
                .GroupBy(
                    example => $"{example.Project}|{example.FilePath}|{example.LineNumber}|{example.Snippet}|{example.Route}",
                    StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .OrderBy(example => GetSourcePriority(example.SourceKind))
                .ThenBy(example => example.Project, StringComparer.OrdinalIgnoreCase)
                .ThenBy(example => example.FilePath, StringComparer.OrdinalIgnoreCase)
                .ThenBy(example => example.LineNumber)
                .ToArray(),
            StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveSourcePath(string sourceRoot, string componentName)
    {
        var componentsRoot = Path.Combine(sourceRoot, "Components");
        var directPath = Path.Combine(componentsRoot, $"{componentName}.razor");
        if (File.Exists(directPath) || !Directory.Exists(componentsRoot))
        {
            return directPath;
        }

        var candidates = Directory.EnumerateFiles(componentsRoot, $"{componentName}.razor", SearchOption.AllDirectories)
            .Where(path => !IsGeneratedPath(path))
            .OrderBy(path => path.Contains($"{Path.DirectorySeparatorChar}Compatibility{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return candidates.FirstOrDefault() ?? directPath;
    }

    private static string GetComponentName(Type type)
    {
        var rawName = type.Name;
        var tickIndex = rawName.IndexOf('`');
        return tickIndex >= 0 ? rawName[..tickIndex] : rawName;
    }

    private static string ResolveCanvasContractKind(string typeName)
    {
        if (typeName.EndsWith("Surface", StringComparison.Ordinal))
        {
            return "surface";
        }

        if (typeName.EndsWith("EventArgs", StringComparison.Ordinal))
        {
            return "event";
        }

        if (typeName.EndsWith("Request", StringComparison.Ordinal))
        {
            return "request";
        }

        if (typeName.EndsWith("State", StringComparison.Ordinal))
        {
            return "state";
        }

        if (typeName.EndsWith("Options", StringComparison.Ordinal))
        {
            return "options";
        }

        return "model";
    }

    private static string BuildCanvasContractSummary(string typeName)
    {
        return ResolveCanvasContractKind(typeName) switch
        {
            "surface" => "Top-level typed surface passed into the shared canvas runtime.",
            "event" => "Event payload emitted by the shared canvas runtime back into .NET.",
            "request" => "Typed request emitted by the shared canvas runtime or expected by its callbacks.",
            "state" => "Persisted or computed state used by the shared canvas runtime.",
            "options" => "Options object that configures a reusable canvas subsystem.",
            _ => "Shared canvas contract model used by the extracted canvas libraries."
        };
    }

    private static bool IsEventCallback(Type type)
    {
        return type == typeof(EventCallback) ||
               (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventCallback<>));
    }

    private static bool IsChildContent(Type type)
    {
        return type == typeof(RenderFragment) ||
               (type.IsGenericType && string.Equals(type.GetGenericTypeDefinition().Name, "RenderFragment`1", StringComparison.Ordinal));
    }

    private static string FormatTypeName(Type type)
    {
        if (Nullable.GetUnderlyingType(type) is { } nullableType)
        {
            return $"{FormatTypeName(nullableType)}?";
        }

        if (type == typeof(string))
        {
            return "string";
        }

        if (type == typeof(bool))
        {
            return "bool";
        }

        if (type == typeof(int))
        {
            return "int";
        }

        if (type == typeof(double))
        {
            return "double";
        }

        if (type == typeof(decimal))
        {
            return "decimal";
        }

        if (type == typeof(object))
        {
            return "object";
        }

        if (type.IsArray)
        {
            return $"{FormatTypeName(type.GetElementType()!)}[]";
        }

        if (type.IsGenericType)
        {
            var typeName = GetComponentName(type);
            var genericArguments = string.Join(", ", type.GetGenericArguments().Select(FormatTypeName));
            return $"{typeName}<{genericArguments}>";
        }

        return type.Name;
    }

    private static SearchScore ScoreComponent(
        ComponentDocument component,
        string query,
        IReadOnlyDictionary<string, ComponentGroupDocument> groupLookup)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return new SearchScore(1, []);
        }

        var score = 0d;
        var matchedParameters = new List<string>();
        score += ScoreText(component.Name, query, exactBoost: 100, containsBoost: 60, tokenBoost: 12);
        score += ScoreText(component.FullName, query, exactBoost: 85, containsBoost: 35, tokenBoost: 8);
        score += ScoreText(component.Summary, query, exactBoost: 20, containsBoost: 18, tokenBoost: 6);
        score += ScoreText(string.Join(' ', component.Tags), query, exactBoost: 15, containsBoost: 12, tokenBoost: 5);
        score += ScoreText(string.Join(' ', component.Guidance.UseFor), query, exactBoost: 18, containsBoost: 14, tokenBoost: 5);
        score += ScoreText(string.Join(' ', component.Guidance.AvoidFor), query, exactBoost: 10, containsBoost: 8, tokenBoost: 3);
        score += ScoreText(string.Join(' ', component.Guidance.CompositionRules), query, exactBoost: 18, containsBoost: 14, tokenBoost: 5);

        foreach (var parameter in component.Parameters)
        {
            var parameterScore = ScoreText(parameter.Name, query, exactBoost: 40, containsBoost: 25, tokenBoost: 8) +
                                 ScoreText(parameter.Type, query, exactBoost: 15, containsBoost: 10, tokenBoost: 4) +
                                 ScoreText(parameter.Summary ?? string.Empty, query, exactBoost: 12, containsBoost: 10, tokenBoost: 4) +
                                 ScoreText(parameter.DefaultValue ?? string.Empty, query, exactBoost: 12, containsBoost: 8, tokenBoost: 3) +
                                 ScoreText(string.Join(' ', parameter.AllowedValues), query, exactBoost: 18, containsBoost: 12, tokenBoost: 4);
            if (parameterScore > 0)
            {
                matchedParameters.Add(parameter.Name);
                score += parameterScore;
            }
        }

        foreach (var eventDocument in component.Events)
        {
            score += ScoreText(eventDocument.Name, query, exactBoost: 24, containsBoost: 16, tokenBoost: 6);
        }

        foreach (var groupKey in component.GroupKeys)
        {
            if (groupLookup.TryGetValue(groupKey, out var group))
            {
                score += ScoreText(group.Title, query, exactBoost: 18, containsBoost: 12, tokenBoost: 5);
                score += ScoreText(group.Summary, query, exactBoost: 8, containsBoost: 6, tokenBoost: 2);
            }
        }

        score += ScoreText(string.Join(' ', component.CssNotes), query, exactBoost: 10, containsBoost: 8, tokenBoost: 3);

        return new SearchScore(score, matchedParameters.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToArray());
    }

    private static double ScoreText(string text, string query, double exactBoost, double containsBoost, double tokenBoost)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(query))
        {
            return 0;
        }

        var normalizedText = text.Trim();
        var normalizedQuery = query.Trim();

        if (string.Equals(normalizedText, normalizedQuery, StringComparison.OrdinalIgnoreCase))
        {
            return exactBoost;
        }

        var score = normalizedText.Contains(normalizedQuery, StringComparison.OrdinalIgnoreCase)
            ? containsBoost
            : 0;

        foreach (var token in normalizedQuery.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (token.Length < 2)
            {
                continue;
            }

            if (normalizedText.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                score += tokenBoost;
            }
        }

        return score;
    }

    private static bool MatchesExample(ComponentExampleDocument example, string query)
    {
        return example.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               example.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               example.Scenario.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               example.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase)) ||
               example.ComponentNames.Any(name => name.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesGroup(ComponentGroupDocument group, string query)
    {
        return group.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               group.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               group.FocusAreas.Any(area => area.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool MatchesCanvasContract(CanvasContractDocument contract, string query)
    {
        return contract.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               contract.Summary.Contains(query, StringComparison.OrdinalIgnoreCase) ||
               contract.Properties.Any(property =>
                   property.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                   property.Type.Contains(query, StringComparison.OrdinalIgnoreCase));
    }

    private static bool ExampleMatchesLibrary(ComponentExampleDocument example, string library, IReadOnlyList<ComponentDocument> components)
    {
        var componentLookup = components.ToDictionary(component => component.Name, StringComparer.OrdinalIgnoreCase);
        return example.ComponentNames
            .Any(componentName =>
                componentLookup.TryGetValue(componentName, out var component) &&
                string.Equals(component.Library, library, StringComparison.OrdinalIgnoreCase));
    }

    private static string GetGroupKeyFromRoute(string route)
    {
        return route.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? route.Trim('/');
    }

    private static IReadOnlyList<ConsumerSourceDescriptor> DiscoverConsumerRoots(string workspaceRoot, string sandboxRoot)
    {
        var srcRoot = Path.Combine(workspaceRoot, "src");
        if (!Directory.Exists(srcRoot))
        {
            return DiscoverExternalConsumerRoots(sandboxRoot);
        }

        var consumerRoots = Directory.EnumerateDirectories(srcRoot)
            .Select(rootPath => new ConsumerSourceDescriptor(
                Path.GetFileName(rootPath),
                ResolveSourceKind(Path.GetFileName(rootPath)),
                rootPath))
            .Where(descriptor => !ConsumerProjectExclusions.Contains(descriptor.ProjectName))
            .Concat(DiscoverExternalConsumerRoots(sandboxRoot))
            .GroupBy(descriptor => descriptor.RootPath, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .Where(descriptor => Directory.EnumerateFiles(descriptor.RootPath, "*.razor", SearchOption.AllDirectories).Any(filePath => !IsGeneratedPath(filePath)))
            .OrderBy(descriptor => GetSourcePriority(descriptor.SourceKind))
            .ThenBy(descriptor => descriptor.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return consumerRoots;
    }

    private static IReadOnlyList<ConsumerSourceDescriptor> DiscoverExternalConsumerRoots(string sandboxRoot)
    {
        if (!Directory.Exists(sandboxRoot))
        {
            return [];
        }

        return
        [
            new ConsumerSourceDescriptor(
                "CanDoItAll.Components.Sandbox",
                "sandbox",
                sandboxRoot)
        ];
    }

    private static string ResolveSourceKind(string projectName)
    {
        return projectName switch
        {
            "CanDoItAll.Web" => "product",
            "CanDoItAll.Components.Sandbox" => "sandbox",
            _ when projectName.StartsWith("CanDoItAll.Modules.", StringComparison.OrdinalIgnoreCase) => "module",
            _ when projectName.StartsWith("CanDoItAll.Components", StringComparison.OrdinalIgnoreCase) => "shared",
            _ => "consumer"
        };
    }

    private static int GetSourcePriority(string sourceKind)
    {
        return sourceKind switch
        {
            "product" => 0,
            "module" => 1,
            "sandbox" => 2,
            "shared" => 3,
            _ => 4
        };
    }

    private static string? ResolveRoute(IReadOnlyList<string> lines)
    {
        foreach (var line in lines)
        {
            var match = RouteDirectiveRegex.Match(line);
            if (match.Success)
            {
                return match.Groups["route"].Value;
            }
        }

        return null;
    }

    private static bool IsGeneratedPath(string filePath)
    {
        return filePath.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase) ||
               filePath.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase);
    }

    private static string? ResolveComponentFamily(string library, string sourcePath)
    {
        if (string.IsNullOrWhiteSpace(sourcePath))
        {
            return null;
        }

        var directoryPath = Path.GetDirectoryName(sourcePath);
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            return null;
        }

        var directory = new DirectoryInfo(directoryPath);
        if (string.Equals(directory.Name, "Compatibility", StringComparison.OrdinalIgnoreCase) &&
            directory.Parent is not null)
        {
            directory = directory.Parent;
        }

        if (string.Equals(library, "CanvasLib", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(directory.Name, "Composition", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(directory.Name, "Interaction", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(directory.Name, "Overlays", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(directory.Name, "Primitives", StringComparison.OrdinalIgnoreCase)))
        {
            return "Graph";
        }

        return directory.Name;
    }

    private static string HumanizeComponentName(string componentName)
    {
        return Regex.Replace(componentName, "(?<!^)([A-Z])", " $1").ToLowerInvariant();
    }

    private static IReadOnlyList<string> ResolveCssSourceFileHints(string library, string componentName, string sourcePath)
    {
        if (string.Equals(library, "CanvasLib", StringComparison.OrdinalIgnoreCase))
        {
            return CanvasLibStylesheets
                .Select(path => ComponentRepositoryPath(Path.Combine(
                    "src",
                    "CanDoItAll.Components.CanvasLib",
                    "wwwroot",
                    path.Replace("_content/CanDoItAll.Components.CanvasLib/", string.Empty).Replace('/', Path.DirectorySeparatorChar))))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        if (string.Equals(library, "Charts", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (string.Equals(library, "Mermaid", StringComparison.OrdinalIgnoreCase))
        {
            return [ComponentRepositoryPath(Path.Combine("src", "CanDoItAll.Components.Mermaid", "Components", "MermaidDiagram.razor.css"))];
        }

        if (!string.Equals(library, "BaseLib", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        if (BaseLibCssSourceFilesByComponent.TryGetValue(componentName, out var explicitFiles))
        {
            return ToComponentRepositoryPaths(explicitFiles);
        }

        var family = ResolveComponentFamily(library, sourcePath);
        if (string.IsNullOrWhiteSpace(family))
        {
            return [];
        }

        IReadOnlyList<string> sourceFiles = family switch
        {
            "Badges" => [@"Tailwind\controls\badges.css"],
            "Buttons" => [@"Tailwind\controls\buttons.css"],
            "Cards" => componentName.Contains("Stat", StringComparison.OrdinalIgnoreCase) ||
                       componentName.Contains("Summary", StringComparison.OrdinalIgnoreCase)
                ? [@"Tailwind\surfaces\cards.css", @"Tailwind\layout\stats.css"]
                : [@"Tailwind\surfaces\cards.css"],
            "DataVisualization" => [@"Tailwind\foundation\radzen-layout.css"],
            "Feedback" => componentName.Contains("Popover", StringComparison.OrdinalIgnoreCase) ||
                          componentName.Contains("Tooltip", StringComparison.OrdinalIgnoreCase)
                ? []
                : [@"Tailwind\feedback\alerts.css"],
            "Forms" => [@"Tailwind\forms\fields.css"],
            "Identity" => [@"Tailwind\typography\text.css"],
            "Layout" => componentName.Contains("Stat", StringComparison.OrdinalIgnoreCase)
                ? [@"Tailwind\layout\stats.css"]
                : [@"Tailwind\layout\stacks.css", @"Tailwind\layout\sheets.css"],
            "Lists" => [@"Tailwind\layout\sheets.css", @"Tailwind\surfaces\cards.css"],
            "Modals" => [],
            "Navigation" => componentName.Contains("Tree", StringComparison.OrdinalIgnoreCase)
                ? [@"Tailwind\navigation\treeview.css"]
                : componentName.Contains("Tab", StringComparison.OrdinalIgnoreCase)
                    ? [@"Tailwind\navigation\tabs.css"]
                    : componentName.Contains("Header", StringComparison.OrdinalIgnoreCase) ||
                      componentName.Contains("Toolbar", StringComparison.OrdinalIgnoreCase) ||
                      componentName.Contains("Filter", StringComparison.OrdinalIgnoreCase) ||
                      componentName.Contains("Toc", StringComparison.OrdinalIgnoreCase)
                        ? [@"Tailwind\navigation\page-header.css"]
                        : [],
            "Storage" => [@"Tailwind\surfaces\cards.css", @"Tailwind\controls\badges.css"],
            "Typography" => [@"Tailwind\typography\text.css"],
            _ => []
        };

        return ToComponentRepositoryPaths(sourceFiles);
    }

    private static IReadOnlyList<string> ToComponentRepositoryPaths(IReadOnlyList<string> relativePaths)
        => relativePaths
            .Select(ComponentRepositoryPath)
            .ToArray();

    private static string ComponentRepositoryPath(string relativePath)
        => Path.Combine(ComponentsRepositoryRelativeRoot, relativePath);

    private static string? BuildParameterSummary(string componentName, string parameterName)
    {
        if (ParameterDescriptionsByComponent.TryGetValue(componentName, out var componentDescriptions) &&
            componentDescriptions.TryGetValue(parameterName, out var parameterSummary))
        {
            return parameterSummary;
        }

        return DefaultParameterDescriptionsByName.TryGetValue(parameterName, out var defaultSummary)
            ? defaultSummary
            : null;
    }

    private static object? TryCreateDefaultInstance(Type type)
    {
        try
        {
            return Activator.CreateInstance(type);
        }
        catch
        {
            return null;
        }
    }

    private static string? ResolveDefaultValue(PropertyInfo property, object? defaultInstance)
    {
        if (defaultInstance is null ||
            property.GetMethod is null ||
            property.GetIndexParameters().Length > 0 ||
            property.PropertyType == typeof(RenderFragment) ||
            (property.PropertyType.IsGenericType && string.Equals(property.PropertyType.GetGenericTypeDefinition().Name, "RenderFragment`1", StringComparison.Ordinal)) ||
            typeof(Delegate).IsAssignableFrom(property.PropertyType))
        {
            return null;
        }

        try
        {
            var value = property.GetValue(defaultInstance);
            return FormatDefaultValue(value, property.PropertyType);
        }
        catch
        {
            return null;
        }
    }

    private static string? FormatDefaultValue(object? value, Type propertyType)
    {
        if (value is null)
        {
            return null;
        }

        var normalizedType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (normalizedType == typeof(string))
        {
            return $"\"{value}\"";
        }

        if (normalizedType == typeof(bool))
        {
            return (bool)value ? "true" : "false";
        }

        if (normalizedType.IsEnum)
        {
            return Enum.GetName(normalizedType, value);
        }

        if (normalizedType == typeof(int) ||
            normalizedType == typeof(long) ||
            normalizedType == typeof(double) ||
            normalizedType == typeof(float) ||
            normalizedType == typeof(decimal))
        {
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        return null;
    }

    private static IReadOnlyList<string> ResolveAllowedValues(Type type)
    {
        var normalizedType = Nullable.GetUnderlyingType(type) ?? type;
        if (!normalizedType.IsEnum)
        {
            return [];
        }

        return Enum.GetNames(normalizedType)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string TruncateSnippet(string snippet)
    {
        return snippet.Length <= 220
            ? snippet
            : $"{snippet[..217]}...";
    }

    private sealed record LibraryDescriptor(string Name, Assembly Assembly, string SourceRoot, string ComponentNamespace);

    private sealed record ConsumerSourceDescriptor(string ProjectName, string SourceKind, string RootPath);

    private sealed record SearchScore(double Score, IReadOnlyList<string> MatchedParameters);
}
