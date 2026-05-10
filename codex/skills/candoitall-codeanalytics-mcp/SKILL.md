---
name: candoitall-codeanalytics-mcp
description: Use when inspecting C# solutions through the CanDoItAll codeanalytics MCP, especially for scoped solution/project inventory, DI, persistence, symbol lookup, file inspection, focused context walking, and SharpTools-style read-only analysis while keeping SharpTools as a disabled backup only.
---

# CanDoItAll CodeAnalytics MCP

## Goal

Use `candoitall_codeanalytics` as the default read-only investigation surface for C# solutions before reaching for SharpTools.

The priority is high-signal context, not maximum context. Build broad snapshots only when the question is truly architecture-wide.

## Baseline Flow

1. Start with `code_analytics_snapshot_build` if you do not already have a usable snapshot for the target solution.
2. Reuse that snapshot id across the investigation instead of rebuilding.
3. For large solutions, avoid a full-solution snapshot unless the question is architecture-wide. Prefer a target `.csproj`, `ScopeProjectNames`, or `ScopeNamespacePrefixes` when the task is localized.
4. Check snapshot health before trusting negative evidence. Use the build result, `code_analytics_dashboard_get`, or inventory counts to confirm projects/types/members loaded. If a snapshot is empty or diagnostics show load failure, report that instead of treating missing symbols as real.
5. Prefer the narrowest tool that directly answers the question.
6. Use `focused_context_get` only when you need stitched multi-hop context. Do not start there when the question names an exact symbol or file.

## Tool Choice

- Snapshot health:
  Use `code_analytics_dashboard_get` after a new or suspicious snapshot to inspect diagnostics, finding counts, and recent snapshot state.
- Solution and project graph:
  Use `code_analytics_solution_inventory_get` for direct project references and reverse references.
  Use `code_analytics_project_inventory_get` when the question is about one project and its files.
  Treat `DirectProjectReferences` and `ReferencedByProjects` as the primary product-project answer path.
  If the caller also cares about tests or benchmarks, inspect `SupportingDirectProjectReferences` and `SupportingReferencedByProjects`.
- DI:
  Use `code_analytics_services_get`.
- Persistence:
  Use `code_analytics_persistence_get`.
- Exact symbol lookup:
  Use `code_analytics_symbols_search` first.
  Use `SearchMode=Exact` for exact type/member names, especially fully qualified names.
  Then use `code_analytics_symbol_definition_get`, `code_analytics_symbol_members_get`, `code_analytics_symbol_implementations_get`, or `code_analytics_symbol_references_get` depending on the question.
- File inspection:
  Use `code_analytics_document_symbols_get` for the type/member outline of a file.
  Use `code_analytics_document_source_get` for raw source text from a file.
- Broader stitched investigation:
  Use `code_analytics_focused_context_get` for trouble paths, usage summaries, representative consumers, or implementation overviews once the seed symbol is known.
  Prefer `TroublePath` explicitly; `Behavior` is legacy compatibility only.
  Use `FocusTags` for architectural area bias. Supported examples include `Db`, `Database`, `EntityFramework`, `EfCore`, `Ui`, `Razor`, `Component`, `Service`, `Domain`, `Model`, `Infra`, `Client`, `Crypto`, `Linq`, `Parser`, `Protocol`, `Query`, `Test`, and `Write`.
  Use `RelationHints` when the task names a second relevant function, class, Razor component, project, namespace, or path. Relation hints narrow helper usage samples instead of asking the agent to inspect every caller.
  Use `Depth` deliberately: `0` for definition-only, `1` for direct relationships, and `2` for a bounded trouble path. Avoid `3+` unless the previous result proves the extra hop is needed.
  Use `Precision=Outline` for orientation, `Precision=Surgical` for exact repairs, and `Precision=Balanced` when both structure and snippets are needed.

## Recommended Sequences

- First steps in an unknown project:
  `snapshot_build` with the narrowest known scope -> `dashboard_get` -> `solution_inventory_get`.
  Then inspect only the relevant project with `project_inventory_get`; do not ask for full source until a target file or symbol is known.
- First steps in a known subsystem:
  `snapshot_build` with `ScopeProjectNames` or `ScopeNamespacePrefixes` -> `project_inventory_get` -> `symbols_search` or `document_symbols_get`.
  Use `focused_context_get` with `Precision=Outline` only after inventory shows the likely entry points.
- Architecture dependency question:
  `snapshot_build` -> `solution_inventory_get`.
  If the answer is about product architecture, use the primary product-reference arrays before mentioning supporting-project arrays.
- Load one project like SharpTools `LoadProject`:
  `snapshot_build` -> `project_inventory_get`.
- Read one file like SharpTools `ReadRawFromRoslynDocument`:
  `snapshot_build` -> `document_source_get`.
- Read file type tree like SharpTools `ReadTypesFromRoslynDocument`:
  `snapshot_build` -> `document_symbols_get`.
- Explain a named method:
  `snapshot_build` -> `symbols_search` with exact or contains search -> `symbol_definition_get`.
  If collaborators are still unclear, follow with `symbol_references_get` or `focused_context_get`.
- Find implementations and consumers:
  `snapshot_build` -> `symbols_search` -> `symbol_implementations_get` -> `symbol_references_get`.
- Helper used in a specific area:
  `snapshot_build` with the narrowest scope available -> `symbols_search` for the helper -> `focused_context_get` with `Intent=UsageSummary` or `Intent=RepresentativeConsumers`, `Depth=1` or `2`, and concrete `RelationHints` naming the target area.
- Helper plus persistence:
  `snapshot_build` -> `symbols_search` for the helper -> `focused_context_get` with the helper seed, `FocusTags=["EntityFramework"]` or `["Db"]`, and relation hints such as the DbContext, repository, entity, or service name.
- Helper plus Razor/component style:
  `snapshot_build` -> `symbols_search` for the helper -> `focused_context_get` with the helper seed, `FocusTags=["Ui"]` or `["Razor"]`, and `RelationHints` naming the component or page.
- Prompt names a file and a symbol:
  `snapshot_build` -> `document_symbols_get` for the file -> `symbols_search` or `symbol_definition_get`.
  Use `document_source_get` only if the symbol excerpt is insufficient.

## Do Not

- Do not use `focused_context_get` as the first step for a clearly named method when `symbol_definition_get` can answer directly.
- Do not read the whole document when `symbol_definition_get` already gives the relevant member body.
- Do not rebuild snapshots repeatedly during one investigation unless source changed, the cache is stale, or the previous snapshot scope was wrong.
- Do not treat the legacy `Behavior` intent as preferred guidance; it is only there to keep stale callers from failing.
- Do not use relation hints as vague natural-language instructions. Provide concrete names such as `AppDbContext`, `StorageCatalogService`, `CanvasSceneHost`, `Workbench`, or a source path segment.
- Do not increase depth to fight noise. Add or tighten `FocusTags`, `RelationHints`, `ProjectName`, or snapshot scope first.
- Do not fall back to SharpTools merely because CodeAnalytics needs a restart or reinstall. Use SharpTools only for a real capability gap.

## Output Expectations

- Cite the snapshot id you used when the investigation is non-trivial.
- Return concrete files, symbols, and direct evidence, not only narrative summaries.
- Say when you had to fall back from a narrow tool to a broader context tool, and why.
- If snapshot diagnostics or counts make the result unreliable, say that explicitly and recommend the narrower rebuild.
