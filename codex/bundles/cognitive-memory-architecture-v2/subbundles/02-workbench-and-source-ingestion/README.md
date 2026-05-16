# 02 Workbench And Source Ingestion

## Status

- Ready after prerequisite gate and module foundation.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
- Convert Workbench and initial source snapshots into deterministic source manifests/items with hashes, layout metadata, links, references, and provenance.

## Covered Inputs

- Requirements FR-001, FR-002, FR-005, FR-022, NFR-005, and NFR-012.
- Mindmap processing architecture and source snapshot prerequisite decision.

## Prerequisites

- `01-module-foundation` must provide durable source manifest and source item models.
- `01a-common-drivers-helpers-and-ef-guardrails` must provide paging, hashing, redaction, fake source providers, and EF query/index rules.
- `14-neuro-foundation-claim-evidence-ledger` must provide evidence anchors and context-frame contracts so ingestion can preserve source anchors and context hints from the start.
- Source snapshot contracts from the prerequisite bundle must exist or be explicitly accepted.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchModels.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchSchemaInitializer.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\WorkbenchProjectStructureRuntimeGateway.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\ProjectStructure\ProjectStructureRuntimeGatewayContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\04-mindmap-processing-architecture.md

## Deliverables

- Workbench source adapter design.
- Source cursor/hash strategy.
- Layout and relation extraction rules.
- Golden source fixture with semantically similar but context-separated nodes.

## Dependency Impact

- Workbench remains authoritative for raw project objects.
- Cognitive Memory stores source references and immutable snapshots, not Workbench-owned state.
- Z coordinates remain metadata-backed unless a later schema migration is approved.

## Validation Depth

- Unit tests for source item keys and content hashes.
- Integration tests for snapshot scan, incremental scan, deletion/tombstone behavior, and cursor resume.
- Performance review for large Workbench surfaces so the adapter does not materialize unbounded source graphs before paging.

## Implementation Steps

- Map Workbench objects, links, notes, assets, metadata, lifecycle events, and layout into source items.
- Persist scan cursors and scan run state.
- Reject duplicate writes through idempotency keys.
- Record source failures with adapter, cursor, exception category, and retry eligibility.

## Do Not Do

- Do not treat Workbench UI layout as the only semantic signal.
- Do not add Cognitive Memory writes into Workbench tables.
- Do not require explicit `PositionZ` schema in V1 unless the design is reopened.

## Acceptance Checklist

- Source scans are resumable.
- Source hashes change only when authoritative source content changes.
- Layout metadata and graph links are available for later recall scoring.

## Proof Required

- Source adapter unit tests.
- Integration test covering first scan and incremental rescan.
- Traceable fixture data committed with expected hashes.

## Browser Validation Logging

- No browser proof is required unless a diagnostic source viewer is added.
- UI browser proof belongs to `08-human-review-ui`.

## Progression Gate

- Proceed to projections only after source snapshots can be replayed deterministically.

## Suggested Agent Prompt

- Implement the Workbench source ingestion path using source snapshot contracts and durable source manifests without changing Workbench behavior.
