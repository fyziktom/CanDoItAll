# Proof Reconciliation Memo

## Summary

The previous `architecture_followup_bundle` closed the Process-module hardening effort too early. Fresh proof reruns on April 13, 2026 show the test matrix it relied on is still green, but the live source still contains the architecture gaps reopened in this bundle.

## Fresh proof

- `integration.trx`: `38` passed from the targeted Process integration matrix.
- `components.trx`: `19` passed from the Process workspace/canvas component matrix.
- `mcp-processes.trx`: `24` passed from the MCP Process test matrix.

## Reconciliation result

- The prior closure claim cannot be reused as-is.
- Passing tests did not falsify the architect's reopened findings.
- The current repository, not the prior execution report, is the source of truth for this follow-up.

## Live mismatches against the prior closure narrative

- Graph legality is still not enforced end to end.
  - `ProcessesService.Support.cs` validates dependency references but not self-loops or multi-step cycles.
  - `ProcessesService.Runtime.cs` still seeds the first step when no legal roots exist.
  - `ProcessCanvasRecompositionService.cs` still appended unresolved nodes after the topological pass before this bundle reopened the issue.
- Runtime uniqueness is still weaker than service assumptions.
  - `ProcessRuntimeEntityConfigurations.cs` still lacks the unique constraints for `(ProcessRunId, StepDefinitionId)` and the logical run-assignment keys.
  - `ProcessesService.Runtime.Operations.cs` still resolves assignments with `FirstOrDefaultAsync` plus insert-on-miss semantics.
- Workspace action ordering is still unsafe.
  - `ProcessWorkspace.DefinitionCrud.cs` flushes pending canvas persistence for some navigation paths, but `PublishAsync`, `DeleteAsync`, and `ExportAsync` still bypass the same quiescence rule.
- The no-draft editor path is still stale-write-vulnerable.
  - `ProcessesService.GetEditorAsync` returns an editor without `DefinitionConcurrencyToken` when a definition exists but no working draft exists.
- Run-details reads are still stitched from multiple service calls.
  - `ProcessWorkspaceRunDetailsLoader.cs` still issues separate reads for step runs, decisions, artifacts, assignments, work briefs, and conformance observations.
- Template mapping logic is still duplicated.
  - `ProcessTemplateCatalogService.cs`, `ProcessTemplateLibraryService.cs`, and `ProcessTemplateProjectionService.cs` still each own overlapping role/artifact projection logic.

## Decision

Subbundle `01-live-proof-reconciliation-and-unverified-closure-reset` is complete because the proof record is now honest and fresh. Downstream work may continue, but only against the reopened findings tracked in `02-open-findings.md`.
