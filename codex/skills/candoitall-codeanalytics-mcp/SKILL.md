---
name: candoitall-codeanalytics-mcp
description: Use when inspecting C# solutions through the CanDoItAll codeanalytics MCP, especially for scoped snapshots, dashboard health, solution/project inventory, dependency and cycle analysis, findings and hotspots, DI registrations, persistence facts, type/member lookup, symbol definitions/references/implementations, file inspection, focused context walking, and SharpTools-style read-only analysis while keeping SharpTools as a disabled backup only.
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
7. For architecture-heavy C# changes, pair this skill with `csharp-architecture-governor`; use CodeAnalytics output as source evidence for the architecture gate, not as a substitute for reading the exact files before editing.

Tool discovery may expose deferred wrapper names, but the tool description and returned `tool` value use the canonical CodeAnalytics names below. Route by capability, not by wrapper suffix.

## Tool Choice

- Snapshot health:
  Use `code_analytics_dashboard_get` after a new or suspicious snapshot to inspect diagnostics, finding counts, and recent snapshot state.
- Solution and project graph:
  Use `code_analytics_solution_inventory_get` for direct project references and reverse references.
  Use `code_analytics_project_inventory_get` when the question is about one project and its files.
  Treat `DirectProjectReferences` and `ReferencedByProjects` as the primary product-project answer path.
  If the caller also cares about tests or benchmarks, inspect `SupportingDirectProjectReferences` and `SupportingReferencedByProjects`.
- Dependencies and cycles:
  Use `code_analytics_dependencies_get` for project, module, namespace, and type dependency facts plus cycles. This is the primary architecture-gate tool for project-reference changes, boundary extraction, cycle risk, and before/after dependency proof.
- Findings and hotspots:
  Use `code_analytics_findings_get` for architectural findings, large-file/type hotspots, open questions, and risk triage. Use this during bundle preparation to identify critical foundation subbundles.
- Exports and recent snapshots:
  Use `code_analytics_exports_get` when a snapshot produced Markdown, Mermaid, or JSON artifacts that should be cited in bundle proof.
  Use `code_analytics_recent_snapshots_get` only to locate a recent usable snapshot; do not prefer stale snapshots over a cheap scoped rebuild.
- DI:
  Use `code_analytics_services_get`.
- Persistence:
  Use `code_analytics_persistence_get`.
- Type and symbol lookup:
  Use `code_analytics_types_search` when the question names a type family, member name inside matching types, or methods-only search.
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
  `snapshot_build` -> `dashboard_get` -> `solution_inventory_get` -> `dependencies_get`.
  If the answer is about product architecture, use the primary product-reference arrays before mentioning supporting-project arrays.
- Architecture bundle preparation:
  `snapshot_build` with architecture-relevant projects or namespaces -> `dashboard_get` -> `findings_get` -> `solution_inventory_get` -> `dependencies_get`.
  Record the snapshot id, scope, diagnostics, top findings, dependency or cycle result, and exact source files that must anchor subbundles.
- Large class, partial class, or runtime responsibility split:
  `snapshot_build` scoped to the project or namespace -> `findings_get` -> `types_search` or `symbols_search` -> `symbol_members_get` -> `focused_context_get` with `Intent=UsageSummary`, `Precision=Outline`, `Depth=1`, and concrete relation hints.
  Read exact definitions only for the target responsibility slice.
- Project-boundary extraction:
  `snapshot_build` scoped to affected projects -> `solution_inventory_get` -> `dependencies_get` -> `symbols_search` for contracts and implementations -> `symbol_references_get`.
  After edits, rebuild or refresh the scoped snapshot and rerun `dependencies_get` for before/after proof.
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
- DI registration question:
  `snapshot_build` with `IncludeDi=true` -> `services_get`.
  If registrations are absent, confirm snapshot diagnostics and inspect registration source with symbol or document tools before concluding there is no registration.
- EF Core or persistence question:
  `snapshot_build` with `IncludePersistence=true` -> `persistence_get` -> exact symbol or document tools for the relevant DbContext, entity, repository, or query handler.
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
- Do not rely on dashboard or findings summaries alone for code changes. Open exact symbol definitions or document source for the files you will edit.
- Do not treat a no-result query as evidence until the dashboard/inventory confirms the snapshot loaded the target projects and documents.
- Do not treat the legacy `Behavior` intent as preferred guidance; it is only there to keep stale callers from failing.
- Do not use relation hints as vague natural-language instructions. Provide concrete names such as `AppDbContext`, `StorageCatalogService`, `CanvasSceneHost`, `Workbench`, or a source path segment.
- Do not increase depth to fight noise. Add or tighten `FocusTags`, `RelationHints`, `ProjectName`, or snapshot scope first.
- Do not use full-solution snapshots for routine symbol lookup in this repo. Scope to projects or namespaces unless the request is explicitly architecture-wide.
- Do not fall back to SharpTools merely because CodeAnalytics needs a restart or reinstall. Use SharpTools only for a real capability gap.

## Output Expectations

- Cite the snapshot id you used when the investigation is non-trivial.
- Return concrete files, symbols, and direct evidence, not only narrative summaries.
- Say when you had to fall back from a narrow tool to a broader context tool, and why.
- If snapshot diagnostics or counts make the result unreliable, say that explicitly and recommend the narrower rebuild.
