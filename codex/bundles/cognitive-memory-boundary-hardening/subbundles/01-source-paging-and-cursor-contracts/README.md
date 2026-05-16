# 01 Source Paging And Cursor Contracts

## Status

- Ready.

## Objective

- Harden memory source paging and cursor contracts so future Cognitive Memory ingestion can scan large sources without unbounded materialization or silent cursor restart.

## Covered Inputs

- H-FR-001, H-FR-002, H-FR-003, H-NFR-002, H-NFR-003, H-NFR-004, and H-NFR-005.
- Raw notes: source providers page after materializing everything; cursor semantics are weak.

## Prerequisites

- Completed `cognitive-memory-prerequisite-boundaries` implementation.
- Existing source snapshot tests must be available as regression proof.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Sources\MemorySourceSnapshotContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\WorkbenchProjectStructureSourceSnapshotProvider.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeEvidenceSourceProvider.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Persistence\WorkflowRuntimeEvidenceSourceProvider.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\WorkbenchSourceSnapshotIntegrationTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\RuntimeEvidenceSourceIntegrationTests.cs

## Deliverables

- Typed cursor contract with source kind, scope, provider/schema version, last item anchor, and stale/invalid status handling.
- Provider paging implementation or explicit bounded-source exception per provider.
- Tests for invalid cursor, stale/deleted cursor, wrong source kind/scope, and bounded page retrieval.
- Documentation in execution report describing any provider that cannot avoid full materialization and why.

## Dependency Impact

- Cognitive Memory source ingestion, consolidation, projection rebuild, and distributed workers depend on trustworthy cursor semantics.
- Weak proof here invalidates later source backfill and incremental scan behavior.

## Validation Depth

- Critical ingestion foundation.
- Integration tests are required for Workbench, Process runtime, and Workflow runtime providers.
- Source review must verify no provider silently restarts from the first item on cursor mismatch.

## Implementation Steps

- Extend cursor/result contracts with typed stale/invalid cursor handling.
- Update `MemorySourceSnapshotPage` or replace it with provider-safe paging helpers.
- Refactor Workbench provider to retrieve bounded pages or document a bounded exception if project structure assembly cannot page yet.
- Refactor Process and Workflow providers to use query-backed page slices where practical.
- Add invalid/stale cursor tests and regression tests for existing stable snapshot behavior.

## Scope Exceptions

- Do not optimize every Process/Workflow query shape beyond the boundary required for safe page retrieval.
- If full snapshot hash requires full materialization, mark it as unavailable, page-scoped, or explicitly expensive rather than silently computing it for every page.

## Do Not Do

- Do not implement Cognitive Memory ingestion.
- Do not add Qdrant projection or recall code.
- Do not silently restart scans on invalid cursors.
- Do not hide source errors behind fallback pages.

## Acceptance Checklist

- Invalid cursor behavior is explicit and tested.
- Providers return bounded pages for large sources or record a justified bounded-source exception.
- Existing source snapshot tests still pass.
- Future Cognitive Memory ingestion can distinguish end-of-page, end-of-source, stale cursor, and provider failure.

## Proof Required

- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~WorkbenchSourceSnapshotIntegrationTests|FullyQualifiedName~RuntimeEvidenceSourceIntegrationTests" --no-restore`
- Source review notes in `reviews/01-execution-report.md`.
- Prepared-stage validator after bundle proof updates if bundle files are changed.

## Browser Validation Logging

- No browser proof is required unless implementation unexpectedly changes visible UI.
- If UI changes occur, record route, viewport, Playwright evidence, and screenshot in `reviews/01-execution-report.md`.

## Progression Gate

- Proceed to redaction/hash hardening only after source paging and cursor behavior are explicit and tested.

## Suggested Agent Prompt

- Implement source paging and cursor hardening only. Make invalid/stale cursor behavior explicit, avoid unbounded provider materialization where practical, update tests, and do not implement Cognitive Memory.
