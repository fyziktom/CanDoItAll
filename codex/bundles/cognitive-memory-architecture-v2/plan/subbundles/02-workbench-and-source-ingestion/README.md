# 02 Workbench And Source Ingestion

## Status

- Completed
- Completion detail: Completed on 2026-05-16.
- Closure gate passed after deterministic source ingestion implementation, targeted unit/integration proof, EF migration proof, static boundary checks, and full solution build.

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

- Source ingestion service consuming existing Workbench, Process runtime, and Workflow runtime source snapshot providers.
- Source cursor/hash/idempotency strategy through typed ingestion requests, source-scan runs, source manifests, and provider-owned cursors.
- Layout, graph-link, context-hint, evidence-anchor, provenance, tombstone, and scan-failure persistence.
- Golden source fixture coverage with Workbench nodes that preserve semantically similar but context-separated source identities and hashes.

## Dependency Impact

- Workbench remains authoritative for raw project objects.
- Cognitive Memory stores source references and immutable snapshots, not Workbench-owned state.
- Z coordinates remain metadata-backed unless a later schema migration is approved.

## Validation Depth

- Unit tests for source item keys and content hashes.
- Integration tests for first scan, duplicate idempotency rejection, incremental rescan, deletion/tombstone behavior, source failure persistence, EF indexes, and source text persistence.
- Static boundary checks confirmed no direct Workbench persistence types, no `PositionZ` dependency, and no forbidden direct-write/upsert/projection markers in the Cognitive Memory module.

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

- Source scans are resumable through provider-owned cursors and idempotent run records.
- Source hashes change only when authoritative source content changes.
- Layout metadata, graph links, context hints, evidence anchors, and tombstones are available for later recall/scoring phases.
- Source snapshot providers remain authoritative; Cognitive Memory stores source references and immutable scan artifacts without writing Workbench-owned state.

## Proof Required

- `tests/CanDoItAll.Tests.Unit/CognitiveMemorySourceIngestionTests.cs`
- `tests/CanDoItAll.Tests.Integration/CognitiveMemorySourceIngestionPersistenceTests.cs`
- `src/CanDoItAll.Migrations.Sqlite/Migrations/20260516182243_AddCognitiveMemorySourceIngestion.cs`
- `src/CanDoItAll.Migrations.PostgreSql/Migrations/20260516182244_AddCognitiveMemorySourceIngestion.cs`
- `dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter "FullyQualifiedName~CognitiveMemory"` passed 33/33.
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~CognitiveMemory"` passed 10/10.
- `dotnet ef migrations has-pending-model-changes` passed for SQLite and PostgreSQL with no model changes.
- `dotnet build CanDoItAll.slnx --no-restore` passed with zero warnings.

## Browser Validation Logging

- No browser proof is required unless a diagnostic source viewer is added.
- UI browser proof belongs to `08-human-review-ui`.

## Progression Gate

- Passed. `03-semantic-and-rag-adapters` may start.
- `04-memory-taxonomy-and-projections` remains blocked until SemanticCompletion/RAG adapter boundaries close.

## Suggested Agent Prompt

- Implement the Workbench source ingestion path using source snapshot contracts and durable source manifests without changing Workbench behavior.
