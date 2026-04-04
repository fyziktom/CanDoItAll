using System.Reflection;
using System.Text.RegularExpressions;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Components.Sandbox;
using CanDoItAll.Mcp.Components.Configuration;
using CanDoItAll.Mcp.Core.Contracts;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Components.Catalog;

public sealed class ComponentCatalogService
{
    private static readonly Regex ComponentReferenceRegex = new(@"<(?<name>[A-Z][A-Za-z0-9_]*)\b", RegexOptions.Compiled);
    private static readonly Regex RouteDirectiveRegex = new(@"^\s*@page\s+""(?<route>[^""]+)""", RegexOptions.Compiled);
    private static readonly IReadOnlyList<string> CanvasLibStylesheets =
    [
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/shell/01-layout-and-shell.css",
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/chrome/02-toolbar-and-windows.css",
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/panels/03-help-settings-and-preview.css",
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/scene/04-scene-and-nodes.css",
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/overlays/05-overlays-and-composer.css",
        "_content/CanDoItAll.Components.CanvasLib/css/workbench/responsive/06-motion-and-responsive.css"
    ];
    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> CssNotesByComponent = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["Button"] =
        [
            "Uses the shared BaseLib button variants, sizes, and tones from `_content/CanDoItAll.Components.BaseLib/css/output.css`.",
            "Prefer `ButtonStyle`, `Variant`, and `Size` before adding page-local button styling."
        ],
        ["PageScaffold"] =
        [
            "Owns page-level spacing and max-width conventions through the shared BaseLib output CSS.",
            "Use scaffold slots before introducing page-local wrapper structure."
        ],
        ["StatusBadge"] =
        [
            "Maps semantic tones to the shared status surface palette in the generated BaseLib stylesheet.",
            "Status chips should communicate state, not replace headings or summaries."
        ],
        ["CanvasWorkbench"] =
        [
            "Uses the shared CanvasLib workbench stylesheets exposed by `<CanvasLibHeadAssets />` under `_content/CanDoItAll.Components.CanvasLib/css/workbench/...` plus the typed `CanvasThemeTokenPack` theme vocabulary.",
            "Toolbar, floating windows, preview cards, and diagnostics share the same `cw-*` token space."
        ],
        ["CanvasCalendar"] =
        [
            "Uses the shared canvas stylesheet and the same theme token pack as the workbench surfaces.",
            "Calendar boundary previews should stay aligned with the runtime token vocabulary rather than inventing parallel styles."
        ],
        ["CanvasBoundaryCard"] =
        [
            "Boundary cards share the same canvas preview card treatment and `cw-*` token space as the runtime previews.",
            "Use boundary cards for proof and documentation surfaces, not for runtime authoring content."
        ]
    };

    private static readonly IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultCssNotesByLibrary = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase)
    {
        ["BaseLib"] =
        [
            "BaseLib components render against `_content/CanDoItAll.Components.BaseLib/css/output.css`.",
            "Typography, spacing, surfaces, and status tones are shared through the BaseLib token system."
        ],
        ["CanvasLib"] =
        [
            "CanvasLib components render against the shared workbench stylesheets exposed by `<CanvasLibHeadAssets />`.",
            "Canvas surfaces also use the typed `CanvasThemeTokenPack` so runtime and preview assets stay aligned."
        ]
    };
    private static readonly IReadOnlyDictionary<string, ComponentGuidanceDocument> GuidanceByComponent = new Dictionary<string, ComponentGuidanceDocument>(StringComparer.OrdinalIgnoreCase)
    {
        ["Grid"] = new(
            [
                "Page and section shells that need explicit tracks.",
                "Responsive layouts that should be driven by `Columns*`, `Rows*`, or `ColumnTemplate*` parameters."
            ],
            [
                "Simple one-dimensional flows that `Stack` already expresses cleanly."
            ],
            [
                "Prefer `Columns*`, `Rows*`, and `ColumnTemplate*` before raw CSS grid utilities.",
                "Use `Gap`, `ColumnGap`, `RowGap`, `AlignItems`, and `JustifyContent` before adding wrapper markup.",
                "Let child `Row` components inherit the parent track variables when the same-row content should collapse with the parent grid."
            ]),
        ["Row"] = new(
            [
                "Nested layout bands inside a `Grid`.",
                "Same-row groups that need their own track count or need to collapse responsively."
            ],
            [
                "Simple horizontal button groups or inline content that `Stack` already handles."
            ],
            [
                "`Row` spans the available grid width and can inherit the parent grid track model automatically.",
                "Use `Columns*` or `ColumnTemplate*` on the row only when that row needs a different track model than its parent.",
                "Use responsive `ColumnsSm` and `ColumnsMd` on the row when sibling columns should wrap under each other."
            ]),
        ["Column"] = new(
            [
                "Content cells inside a `Row`.",
                "Leaf layout containers that need local flex alignment plus a grid placement."
            ],
            [
                "Standalone page shells where `Stack`, `SectionCard`, or `Grid` would express the intent more clearly."
            ],
            [
                "Treat `Column` as both a grid item and a local flex container.",
                "Use `Orientation`, `Gap`, `AlignItems`, and `JustifyContent` instead of ad-hoc flex wrappers.",
                "Use `Size*` only when the row is using span-based sizing; otherwise let inherited track definitions place the column."
            ]),
        ["Stack"] = new(
            [
                "One-dimensional vertical or horizontal flows.",
                "Grouped copy, pill rows, action clusters, and compact sidebars."
            ],
            [
                "Two-dimensional layouts that need explicit tracks across breakpoints."
            ],
            [
                "Reach for `Stack` first when the structure is linear.",
                "When a panel starts needing left/right regions or breakpoint-driven track changes, move to `Grid` or `Row` plus `Column`.",
                "Use `GapScale`, `Wrap`, `AlignItems`, and `JustifyContent` before custom flex classes."
            ]),
        ["FormRow"] = new(
            [
                "Standard paired or evenly split form fields.",
                "Form surfaces that should follow the shared label and spacing rhythm."
            ],
            [
                "Complex asymmetric layouts where `Grid` or `Row` provides clearer control."
            ],
            [
                "Use `FormRow` for common field pairings before creating a custom form wrapper.",
                "Combine `FormRow` with `FormField` so label, helper text, and control spacing stay inside BaseLib."
            ]),
        ["SectionHead"] = new(
            [
                "Section titles, lead copy, and compact header blocks above a panel."
            ],
            [
                "Decorative hero shells that should be handled by page-level layout components."
            ],
            [
                "Use `SectionHead` to keep heading rhythm consistent before adding panel-local typography markup.",
                "Let `SectionHead` own the section introduction so the panel body can focus on content."
            ]),
        ["StatsGrid"] = new(
            [
                "Dashboard-style metric rows and summary tiles."
            ],
            [
                "Arbitrary card mosaics with mixed content density."
            ],
            [
                "Use `StatsGrid` when the content is truly metric-first.",
                "If the cards become multi-purpose sections, switch back to `Grid` plus panel components."
            ])
    };
    private static readonly IReadOnlyDictionary<string, ComponentGuidanceDocument> DefaultGuidanceByLibrary = new Dictionary<string, ComponentGuidanceDocument>(StringComparer.OrdinalIgnoreCase)
    {
        ["BaseLib"] = new(
            [
                "Shared app structure and interactive UI surfaces."
            ],
            [
                "Page-local structural CSS when the shared component parameters already express the intent."
            ],
            [
                "Prefer component parameters, variants, and layout primitives before custom structural classes.",
                "Use sandbox and product examples to mirror established composition patterns."
            ]),
        ["CanvasLib"] = new(
            [
                "Workbench, canvas, and runtime preview surfaces."
            ],
            [
                "Generic page shells that should stay in BaseLib."
            ],
            [
                "Keep runtime and preview surfaces aligned with the shared canvas contracts and token pack."
            ])
    };
    private static readonly IReadOnlySet<string> ConsumerProjectExclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "CanDoItAll.ComponentKit",
        "CanDoItAll.Components.BaseLib",
        "CanDoItAll.Components.CanvasLib",
        "CanDoItAll.Components.Common",
        "CanDoItAll.Mcp.Components",
        "CanDoItAll.Mcp.Core",
        "CanDoItAll.Mcp.DotNetWatch",
        "CanDoItAll.Mcp.LocalRuntime",
        "CanDoItAll.Mcp.ProjectStructure",
        "CanDoItAll.Mcp.SshOps"
    };

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
            _ => new[] { "_content/CanDoItAll.Components.BaseLib/css/output.css" }
        };

        return new ComponentCssTokensData(resolvedComponent.Name, resolvedComponent.Library, stylesheets, resolvedComponent.CssNotes);
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
        var sandboxRoot = Path.GetFullPath(Path.Combine(workspaceRoot, options.Catalog.SandboxRoot));

        var groups = BuildGroups();
        var groupLookup = groups.ToDictionary(group => group.Key, StringComparer.OrdinalIgnoreCase);
        var examples = BuildExamples(groupLookup);

        var libraries = new[]
        {
            new LibraryDescriptor("BaseLib", typeof(Button).Assembly, baseLibRoot),
            new LibraryDescriptor("CanvasLib", typeof(CanvasWorkbench).Assembly, canvasLibRoot)
        };

        var componentTypes = libraries
            .SelectMany(library => DiscoverComponentTypes(library).Select(type => (Library: library, Type: type)))
            .OrderBy(item => GetComponentName(item.Type), StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var componentNames = componentTypes
            .Select(item => GetComponentName(item.Type))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var usageExamplesByComponent = BuildConsumerUsageExamples(workspaceRoot, componentNames);

        var components = componentTypes
            .Select(item => BuildComponentDocument(item.Library, item.Type, examples, groupLookup, componentNames, usageExamplesByComponent))
            .ToArray();

        var canvasContracts = BuildCanvasContracts();

        _ = sandboxRoot;

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
                string.Equals(type.Namespace, type.Assembly == typeof(Button).Assembly
                    ? "CanDoItAll.Components.BaseLib"
                    : "CanDoItAll.Components.CanvasLib", StringComparison.Ordinal));
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

        var tags = relatedExamples
            .SelectMany(example => example.Tags)
            .Concat(groupKeys)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var dependencyNames = DiscoverDependencies(sourceText, componentNames, componentName);
        var (parameters, events) = BuildParameterDocuments(type);
        var cssNotes = BuildCssNotes(componentName, library.Name);
        var guidance = BuildGuidance(componentName, library.Name);
        var usageExamples = usageExamplesByComponent.TryGetValue(componentName, out var matches)
            ? matches
            : [];

        return new ComponentDocument(
            componentName,
            type.FullName ?? componentName,
            type.Namespace ?? string.Empty,
            library.Name,
            BuildSummary(componentName, library.Name, groupKeys, groupLookup, relatedExamples),
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

    private static (IReadOnlyList<ComponentParameterDocument> Parameters, IReadOnlyList<ComponentEventDocument> Events) BuildParameterDocuments(Type type)
    {
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
                IsChildContent(property.PropertyType)))
            .ToArray();

        var events = parameterProperties
            .Where(property => IsEventCallback(property.PropertyType))
            .Select(property => new ComponentEventDocument(property.Name, FormatTypeName(property.PropertyType)))
            .ToArray();

        return (parameters, events);
    }

    private static IReadOnlyList<string> DiscoverDependencies(string sourceText, IReadOnlySet<string> componentNames, string componentName)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return [];
        }

        return ComponentReferenceRegex
            .Matches(sourceText)
            .Select(match => match.Groups["name"].Value)
            .Where(name => componentNames.Contains(name) && !string.Equals(name, componentName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static string BuildSummary(
        string componentName,
        string library,
        IReadOnlyList<string> groupKeys,
        IReadOnlyDictionary<string, ComponentGroupDocument> groupLookup,
        IReadOnlyList<ComponentExampleDocument> examples)
    {
        if (groupKeys.Count > 0)
        {
            var primaryGroup = groupLookup[groupKeys[0]];
            return $"Shared {library} component used in the sandbox {primaryGroup.Title} group. {primaryGroup.Summary}";
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
            return $"Shared {library} component with curated sandbox coverage through {examples[0].Title}.";
        }

        return $"Shared {library} component in the extracted component libraries.";
    }

    private static IReadOnlyList<string> BuildCssNotes(string componentName, string library)
    {
        if (CssNotesByComponent.TryGetValue(componentName, out var notes))
        {
            return notes;
        }

        return DefaultCssNotesByLibrary.TryGetValue(library, out var defaultNotes)
            ? defaultNotes
            : [];
    }

    private static ComponentGuidanceDocument BuildGuidance(string componentName, string library)
    {
        if (GuidanceByComponent.TryGetValue(componentName, out var guidance))
        {
            return guidance;
        }

        return DefaultGuidanceByLibrary.TryGetValue(library, out var defaultGuidance)
            ? defaultGuidance
            : new ComponentGuidanceDocument([], [], []);
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<ComponentUsageExampleDocument>> BuildConsumerUsageExamples(
        string workspaceRoot,
        IReadOnlySet<string> componentNames)
    {
        var usageLookup = new Dictionary<string, List<ComponentUsageExampleDocument>>(StringComparer.OrdinalIgnoreCase);

        foreach (var consumerRoot in DiscoverConsumerRoots(workspaceRoot))
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
        return Path.Combine(sourceRoot, "Components", $"{componentName}.razor");
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
                                 ScoreText(parameter.Type, query, exactBoost: 15, containsBoost: 10, tokenBoost: 4);
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

    private static IReadOnlyList<ConsumerSourceDescriptor> DiscoverConsumerRoots(string workspaceRoot)
    {
        var srcRoot = Path.Combine(workspaceRoot, "src");
        if (!Directory.Exists(srcRoot))
        {
            return [];
        }

        return Directory.EnumerateDirectories(srcRoot)
            .Select(rootPath => new ConsumerSourceDescriptor(
                Path.GetFileName(rootPath),
                ResolveSourceKind(Path.GetFileName(rootPath)),
                rootPath))
            .Where(descriptor => !ConsumerProjectExclusions.Contains(descriptor.ProjectName))
            .Where(descriptor => Directory.EnumerateFiles(descriptor.RootPath, "*.razor", SearchOption.AllDirectories).Any(filePath => !IsGeneratedPath(filePath)))
            .OrderBy(descriptor => GetSourcePriority(descriptor.SourceKind))
            .ThenBy(descriptor => descriptor.ProjectName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
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

    private static string TruncateSnippet(string snippet)
    {
        return snippet.Length <= 220
            ? snippet
            : $"{snippet[..217]}...";
    }

    private sealed record LibraryDescriptor(string Name, Assembly Assembly, string SourceRoot);

    private sealed record ConsumerSourceDescriptor(string ProjectName, string SourceKind, string RootPath);

    private sealed record SearchScore(double Score, IReadOnlyList<string> MatchedParameters);
}
