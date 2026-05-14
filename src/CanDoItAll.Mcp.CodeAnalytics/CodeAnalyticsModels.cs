using System.ComponentModel;
using CanDoItAll.CodeAnalytics.Abstractions;

namespace CanDoItAll.Mcp.CodeAnalytics;

public sealed class CodeAnalyticsBuildSnapshotInput
{
    [Description("Absolute or repository-relative path to the solution or project to analyze. When omitted, the server default is used.")]
    public string? SolutionPath { get; init; }

    [Description("Optional project names to include in the snapshot scope. Prefer this for large solutions when the task is localized; leave empty only for architecture-wide analysis.")]
    public IReadOnlyList<string>? ScopeProjectNames { get; init; }

    [Description("Optional namespace prefixes to include in the snapshot scope. Prefer this for large solutions when the target area is already known.")]
    public IReadOnlyList<string>? ScopeNamespacePrefixes { get; init; }

    [Description("Whether to collect dependency-injection registrations.")]
    public bool IncludeDi { get; init; } = true;

    [Description("Whether to collect EF Core persistence facts.")]
    public bool IncludePersistence { get; init; } = true;

    [Description("Whether to compute architectural risks and findings.")]
    public bool IncludeRisks { get; init; } = true;

    [Description("Whether to include XML documentation summaries in the collected facts.")]
    public bool IncludeXmlDocs { get; init; } = true;

    [Description("Whether to render Markdown and Mermaid export artifacts.")]
    public bool IncludeMermaidExports { get; init; } = true;

    [Description("Whether to bypass the deterministic snapshot cache and rebuild from source.")]
    public bool ForceRefresh { get; init; }
}

public sealed class CodeAnalyticsDashboardQueryInput
{
    [Description("Snapshot identifier returned by a previous snapshot build.")]
    public string SnapshotId { get; init; } = string.Empty;

    [Description("Maximum number of recent snapshots to include alongside the dashboard summary.")]
    public int RecentTake { get; init; } = 10;
}

public sealed class CodeAnalyticsSnapshotQueryInput
{
    [Description("Snapshot identifier returned by a previous snapshot build.")]
    public string SnapshotId { get; init; } = string.Empty;

    [Description("Optional free-text filter applied by the underlying query.")]
    public string? SearchText { get; init; }
}

public sealed class CodeAnalyticsSolutionInventoryInput
{
    [Description("Snapshot identifier returned by a previous snapshot build.")]
    public string SnapshotId { get; init; } = string.Empty;

    [Description("Whether to include document rows for each project.")]
    public bool IncludeDocuments { get; init; }
}

public sealed class CodeAnalyticsProjectInventoryInput
{
    [Description("Snapshot identifier returned by a previous snapshot build.")]
    public string SnapshotId { get; init; } = string.Empty;

    [Description("Optional project identifier returned by a project-aware query.")]
    public string? ProjectId { get; init; }

    [Description("Optional project name when a stable project identifier is not available.")]
    public string? ProjectName { get; init; }

    [Description("Whether to include document rows for the selected project.")]
    public bool IncludeDocuments { get; init; } = true;
}

public sealed class CodeAnalyticsTypeSearchInput
{
    [Description("Snapshot identifier returned by a previous snapshot build.")]
    public string SnapshotId { get; init; } = string.Empty;

    [Description("Optional type-name filter.")]
    public string? SearchText { get; init; }

    [Description("Optional project-name filter.")]
    public string? ProjectName { get; init; }

    [Description("Optional member-name filter applied within matching types.")]
    public string? MemberSearchText { get; init; }

    [Description("Whether to include matching members in the response.")]
    public bool IncludeMembers { get; init; } = true;

    [Description("Whether to restrict member matches to methods only.")]
    public bool MethodsOnly { get; init; }
}

public sealed class CodeAnalyticsSymbolSearchInput
{
    [Description("Snapshot identifier returned by a previous snapshot build.")]
    public string SnapshotId { get; init; } = string.Empty;

    [Description("Search text used to match types and members. Use fully qualified or exact names with SearchMode=Exact when the prompt names a concrete symbol.")]
    public string SearchText { get; init; } = string.Empty;

    [Description("Optional project-name filter.")]
    public string? ProjectName { get; init; }

    [Description("How to interpret SearchText: Contains, Exact, or Regex.")]
    public SymbolSearchMode SearchMode { get; init; } = SymbolSearchMode.Contains;

    [Description("Whether to search types.")]
    public bool IncludeTypes { get; init; } = true;

    [Description("Whether to search members.")]
    public bool IncludeMembers { get; init; } = true;

    [Description("Maximum number of results to return.")]
    public int Take { get; init; } = 40;
}

public sealed class CodeAnalyticsSymbolTargetInput
{
    [Description("Snapshot identifier returned by a previous snapshot build.")]
    public string SnapshotId { get; init; } = string.Empty;

    [Description("Type identifier returned by a type or symbol search.")]
    public string TypeId { get; init; } = string.Empty;

    [Description("Optional member identifier returned by a symbol search. Leave empty to target the type itself when supported.")]
    public string? MemberId { get; init; }
}

public sealed class CodeAnalyticsSymbolReferencesInput
{
    [Description("Snapshot identifier returned by a previous snapshot build.")]
    public string SnapshotId { get; init; } = string.Empty;

    [Description("Type identifier returned by a type or symbol search.")]
    public string TypeId { get; init; } = string.Empty;

    [Description("Optional member identifier returned by a symbol search. Leave empty to gather references to the type.")]
    public string? MemberId { get; init; }

    [Description("Maximum number of scored references to return.")]
    public int Take { get; init; } = 40;
}

public sealed class CodeAnalyticsFocusedContextInput
{
    [Description("Snapshot identifier returned by a previous snapshot build.")]
    public string SnapshotId { get; init; } = string.Empty;

    [Description("Optional type identifier returned by a type or symbol search.")]
    public string? TypeId { get; init; }

    [Description("Optional member identifier returned by a symbol search.")]
    public string? MemberId { get; init; }

    [Description("Optional service-registration identifier from a services query.")]
    public string? ServiceRegistrationId { get; init; }

    [Description("Traversal depth from 0 to 5. Prefer 0 for definition-only, 1 for direct relationships, and 2 for bounded trouble paths; use 3+ only after a narrower result proves it is needed.")]
    public int Depth { get; init; } = 2;

    [Description("Optional free-text hint that helps the service pick the most relevant context. Prefer TypeId or MemberId when an exact symbol is already known.")]
    public string? QueryText { get; init; }

    [Description("Optional focus tags that bias selected context. Supported examples include Db, EntityFramework, Ui, Razor, Service, Domain, Model, Infra, Client, Crypto, Linq, Parser, Protocol, Query, Test, and Write.")]
    public IReadOnlyList<string>? FocusTags { get; init; }

    [Description("Optional concrete related classes, methods, components, projects, namespaces, or paths that narrow representative usages. Use names like AppDbContext, StorageCatalogService, CanvasSceneHost, or a source path segment.")]
    public IReadOnlyList<string>? RelationHints { get; init; }

    [Description("What kind of focused context to assemble: Definition, Implementations, UsageSummary, RepresentativeConsumers, TroublePath, or Auto.")]
    public FocusedContextIntent Intent { get; init; } = FocusedContextIntent.Auto;

    [Description("How aggressively to compress selected context. Use Outline for first-pass orientation, Surgical for exact repairs, and Balanced when both shape and snippets are needed.")]
    public FocusedContextPrecision Precision { get; init; } = FocusedContextPrecision.Auto;
}

public sealed class CodeAnalyticsDocumentTargetInput
{
    [Description("Snapshot identifier returned by a previous snapshot build.")]
    public string SnapshotId { get; init; } = string.Empty;

    [Description("Optional document identifier returned by a document-aware query.")]
    public string? DocumentId { get; init; }

    [Description("Optional document path. Supports snapshot-relative or absolute paths.")]
    public string? DocumentPath { get; init; }
}
