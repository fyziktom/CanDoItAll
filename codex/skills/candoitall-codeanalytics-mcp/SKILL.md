---
name: candoitall-codeanalytics-mcp
description: Use when inspecting C# solutions through the CanDoItAll codeanalytics MCP, especially for solution/project inventory, DI, persistence, symbol lookup, file inspection, and SharpTools-style read-only analysis without falling back to SharpTools.
---

# CanDoItAll CodeAnalytics MCP

## Goal

Use `candoitall_codeanalytics` as the default read-only investigation surface for C# solutions before reaching for SharpTools.

## Baseline Flow

1. Start with `code_analytics_snapshot_build` if you do not already have a usable snapshot for the target solution.
2. Reuse that snapshot id across the investigation instead of rebuilding.
3. Prefer the narrowest tool that directly answers the question.
4. Use `focused_context_get` only when you need stitched multi-hop context. Do not start there when the question names an exact symbol or file.

## Tool Choice

- Solution and project graph:
  Use `code_analytics_solution_inventory_get` for direct project references and reverse references.
  Use `code_analytics_project_inventory_get` when the question is about one project and its files.
- DI:
  Use `code_analytics_services_get`.
- Persistence:
  Use `code_analytics_persistence_get`.
- Exact symbol lookup:
  Use `code_analytics_symbols_search` first.
  Then use `code_analytics_symbol_definition_get`, `code_analytics_symbol_members_get`, `code_analytics_symbol_implementations_get`, or `code_analytics_symbol_references_get` depending on the question.
- File inspection:
  Use `code_analytics_document_symbols_get` for the type/member outline of a file.
  Use `code_analytics_document_source_get` for raw source text from a file.
- Broader stitched investigation:
  Use `code_analytics_focused_context_get` for trouble paths, usage summaries, representative consumers, or implementation overviews once the seed symbol is known.

## Recommended Sequences

- Architecture dependency question:
  `snapshot_build` -> `solution_inventory_get`
- Load one project like SharpTools `LoadProject`:
  `snapshot_build` -> `project_inventory_get`
- Read one file like SharpTools `ReadRawFromRoslynDocument`:
  `snapshot_build` -> `document_source_get`
- Read file type tree like SharpTools `ReadTypesFromRoslynDocument`:
  `snapshot_build` -> `document_symbols_get`
- Explain a named method:
  `snapshot_build` -> `symbols_search` with exact or contains search -> `symbol_definition_get`
  If collaborators are still unclear, follow with `symbol_references_get` or `focused_context_get`.
- Find implementations and consumers:
  `snapshot_build` -> `symbols_search` -> `symbol_implementations_get` -> `symbol_references_get`

## Do Not

- Do not use `focused_context_get` as the first step for a clearly named method when `symbol_definition_get` can answer directly.
- Do not read the whole document when `symbol_definition_get` already gives the relevant member body.
- Do not rebuild snapshots repeatedly during one investigation unless source changed or the cache is stale.

## Output Expectations

- Cite the snapshot id you used when the investigation is non-trivial.
- Return concrete files, symbols, and direct evidence, not only narrative summaries.
- Say when you had to fall back from a narrow tool to a broader context tool, and why.
