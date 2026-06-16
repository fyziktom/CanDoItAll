# SB14 Definition List, Scope Tree, Search, And Feed Defaults

## Status

Completed on 2026-06-16. Implemented and validated in dependency order after SB13.

## Objective

Rebuild the Process definition catalog area: counters, global/project scope tree, search, visible definition filtering, selection, empty states, and Feed Defaults command over projection/application contracts.

## Covered Inputs

- REQ-030, REQ-031, REQ-051, REQ-052.
- US-001 through US-004.
- AC-021, AC-022, AC-035, AC-039, AC-040.

## Prerequisites

- SB13 UI shell and projection client foundation complete.
- SB12 template migration/indexing complete enough for default feed behavior.

## Exact Source References

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `repo://Templates/Processes/manifest.json`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Components/ProcessWorkspaceTests.cs`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/processes-page-workspace-1600x1000.png`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Definition catalog projection UI with counters, scope grouping, search, and selection.
- Feed Defaults command wired to template import service with command receipt and refresh token.
- Component tests for scope/search/selection/feed states.
- Playwright proof for browse, search, select, and feed defaults flow.

## Dependency Impact

- SB15 definition editor and SB19 template library depend on stable definition selection and catalog refresh.

## Validation Depth

- Projection query tests for scope/search semantics.
- Component tests for empty, loading, selected, filtered, and command-result states.
- Playwright proof for visible definition filtering and selection.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Implement definition catalog projection query usage in the UI.
2. Render global and project scope groupings with counts and current selection.
3. Add search and clear behavior without client-side runtime truth derivation.
4. Wire Feed Defaults to a typed application command.
5. Add component and Playwright tests.
6. Record story coverage for US-001 through US-004.

## Do Not Do

- Do not import templates by reading Markdown or Mermaid as canonical data.
- Do not query runtime tables for definition counts.
- Do not implement definition editing forms in this bundle.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [x] Definition catalog uses projection DTOs.
- [x] Search and scope filtering work predictably.
- [x] Feed Defaults produces a command receipt and projection refresh.
- [x] Current workspace behavior is covered by tests and screenshots.

## Proof Required

- Component and projection test output.
- Playwright screenshot and action log.
- Story coverage table for US-001 through US-004.

## Browser Validation Logging

- Required. Capture `/processes`, definition search, selection, Feed Defaults action, screenshot, and console/network summary.

## Progression Gate

- SB15 may start. Definition selection and refresh behavior are stable.

## Suggested Agent Prompt

Execute SB14 from `codex/bundles/process-module-architecture-v3/subbundles/14-definition-list-scope-tree-search-and-feed-defaults`. Rebuild the definition catalog and Feed Defaults flow over projections and typed commands.

## Handoff Notes For Next Bundle

- Selection key: `ProcessDefinitionCatalogItemKey`.
- Query state: `ProcessDefinitionCatalogQueryProjection(SearchText, SelectedDefinitionKey, ScopeFilter, Take)`.
- Selected metadata available to SB15: key, name, summary, scope kind, status, criticality, operating mode, updated timestamp, and compatibility issue count.
- Feed Defaults returns `ProcessDefinitionCatalogCommandReceipt` with receipt id, command kind, status, refresh token, affected definition count, accepted timestamp, and summary.
- Template defaults load from canonical JSON through `ProcessTemplatePackLoader` using `ProcessTemplateJsonContext` source-generated metadata.
- Project-specific definitions are represented explicitly by the project scope group but currently have count 0 until the SB15/SB19 persistence/editing path exists.

## Completion Proof

- `bundle://proof/SB14/manifest.md`
- `bundle://proof/SB14/build-solution-sb14.txt`
- `bundle://proof/SB14/test-unit-definition-catalog-sb14.txt`
- `bundle://proof/SB14/test-components-process-shell-sb14.txt`
- `bundle://proof/SB14/test-playwright-process-shell-sb14.txt`
- `bundle://proof/SB14/browser-validation.md`
- `bundle://proof/SB14/codeanalytics-snapshot-summary.txt`
