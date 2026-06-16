# SB19 Template Library Browser, Preview, And Selective Import

## Status

Completed on 2026-06-16. Implemented and validated as SB19 during the approved architecture bundle execution.

## Objective

Rebuild the template library dialog for process, role, and artifact templates with category/search browsing, overview/Markdown/diagram/JSON/structure previews, related components, and selective import into a selected definition.

## Covered Inputs

- REQ-031 to REQ-037, REQ-051, REQ-052.
- US-021 through US-023.
- AC-022 to AC-026, AC-039, AC-040.

## Prerequisites

- SB12 template migration/indexing complete.
- SB18 target-step artifact mapping complete.

## Exact Source References

- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/src/CanDoItAll.Modules.Processes/Components/ProcessTemplateLibraryDialog.razor`
- `repo://Templates/Processes/manifest.json`
- `repo://codex/bundles/process-module-rewrite-reference-v1/legacy/tests/CanDoItAll.Tests.Playwright/AppSmokeTests.ProcessManagementBundle.cs`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/processes-template-library-dialog-1600x1000.png`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Template library UI over `TemplateCatalogProjection`.
- Preview tabs for overview, generated Markdown, generated Mermaid/diagrams, canonical JSON, and structure tree.
- Selective import commands for process, role, and artifact components.
- Component and Playwright proof for browsing, previewing, and importing.

## Dependency Impact

- SB20 exchange/Git UI depends on template component identity and conflict metadata.
- SB28 depends on template story regression evidence.

## Validation Depth

- Template projection tests proving JSON canonical source.
- Component tests for search/category/preview/import states.
- Playwright proof for template library open, preview tabs, and selective import.

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

1. Bind the dialog to template catalog projections and typed import commands.
2. Render search, categories, counts, preview panel, preview tabs, and structure tree.
3. Implement role/artifact selective import with target step validation.
4. Display migration/source metadata and import warnings.
5. Add component and Playwright tests.
6. Record story coverage for US-021 through US-023.

## Do Not Do

- Do not treat Markdown or Mermaid as canonical source.
- Do not bypass template migration/upcast services.
- Do not implement Git merge/conflict resolution in this bundle.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [x] Template catalog renders from JSON-backed projections.
- [x] Preview tabs work and identify generated projections.
- [x] Selective import works for process, role, and artifact components.
- [x] Playwright proof exists.

## Proof Required

- Template projection/component test output.
- Playwright template library screenshot evidence.
- Story coverage table for US-021 through US-023.

## Closure Proof

- `bundle://proof/SB19/manifest.md`
- `bundle://proof/SB19/semantic-invariants.md`
- `bundle://proof/SB19/story-coverage.md`
- `bundle://proof/SB19/browser-validation.md`
- `bundle://proof/SB19/codeanalytics-snapshot-summary.txt`
- `bundle://proof/SB19/subbundle-closure-gate-sb19.md`

## Browser Validation Logging

- Required. Capture dialog open, search/category action, preview tab action, import action, screenshot, and console/network summary.

## Progression Gate

- SB20 may start after template component identity and import metadata are stable.

## Suggested Agent Prompt

Execute SB19 from `codex/bundles/process-module-architecture-v3/subbundles/19-template-library-browser-preview-and-selective-import`. Rebuild the template library over JSON-backed projections and typed import commands.

## Handoff Notes For Next Bundle

SB20 can consume typed template catalog item identity, imported component source definition/component keys, canonical source hashes, target-step artifact import metadata, and stale-version rejection proof. SB20 still owns Git exchange, diff, merge, and conflict UI.
