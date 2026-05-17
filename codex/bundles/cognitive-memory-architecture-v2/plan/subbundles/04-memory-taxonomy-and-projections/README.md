# 04 Memory Taxonomy And Projections

## Status

- Passed on 2026-05-16.

## Execution Control

- Before editing code, update `C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\checklists\cognitive-memory-implementation-control.xlsx`.
- Mark this subbundle `In Progress`, verify prerequisite rows are `Passed`, and record target branch/commit.
- During implementation, update owned checklist rows and proof paths.
- Before closure, update workbook `Phase Gates`, `Phase Acceptance Checklist`, `Validation Evidence`, `Handoff Log`, and `reviews/01-execution-report.md`.
- If evidence is missing or an upstream assumption fails, mark the subbundle `Blocked` and stop downstream work.
## Objective
- Build durable canonical memory records, typed relations, projection lifecycle state, and rebuild semantics.

## Covered Inputs

- Requirements FR-003, FR-004, FR-006, FR-007, FR-009, NFR-001, NFR-002, NFR-007, and NFR-009.
- Memory taxonomy, data model, and projection architecture.

## Prerequisites

- `02-workbench-and-source-ingestion` supplies deterministic source records.
- `03-semantic-and-rag-adapters` supplies projection and semantic utilities.
- `01a-common-drivers-helpers-and-ef-guardrails` supplies typed state/profile/evidence contracts, JSON rules, and EF query/index policy.
- `01b-score-geometry-driver` supplies score-space definitions for memory activation, relation confidence, mindmap similarity, and projection-derived similarity dimensions.
- `14-neuro-foundation-claim-evidence-ledger` must define claims, evidence anchors, entity/context binding, mutation authority, and typed projection payload rules.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\03-memory-taxonomy-and-data-model.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\07-qdrant-projection-design.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\MemoryDomainModels.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\ProjectionContracts.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Persistence\AppDbContextModelRegistry.cs

## Deliverables

- Canonical memory item and relation model.
- Projection state model with source hash, projection hash, embedding profile, and algorithm version.
- Rebuild, tombstone, and stale projection rules.

## Dependency Impact

- Persistence must support large data without mixing projections into canonical records.
- RAG/Qdrant adapter consumes projection payloads and reports state.
- Recall consumes typed relations and projection availability.

## Validation Depth

- Unit tests for taxonomy invariants and relation constraints.
- Integration tests for projection rebuild and stale projection detection.
- EF model tests proving query-relevant refs, relations, review state, projection state, and evidence lookup are indexed and not hidden only in JSON.
- Score geometry model tests proving confidence, activation, relation strength, and projection similarity are vector/shape-backed where they affect behavior.

## Implementation Steps

- Define canonical memory types and relation types.
- Add projection lifecycle records.
- Implement deterministic projection payload generation.
- Add rebuild and stale cleanup workflows.

## Do Not Do

- Do not make generated summaries authoritative source.
- Do not merge semantically similar but context-separated records automatically.
- Do not allow projection failure to corrupt durable memory state.

## Acceptance Checklist

- Passed: canonical memory projection requests require source links and evidence anchors; record-to-evidence-anchor links are queryable rows.
- Passed: projection records are item-level, deleteable, rebuildable, and keyed by store/kind/profile/source hash/payload hash.
- Passed: relation confidence and source evidence are explicit through score trace ids, buckets, relation evidence rows, and evidence anchors.
- Passed: projection/activation/relation strength use score trace hooks and display-only projections; no final rank/final score production surface was added.

## Proof Required

- `src/CanDoItAll.Modules.CognitiveMemory/Taxonomy/CognitiveMemoryTaxonomyContracts.cs`
- `src/CanDoItAll.Modules.CognitiveMemory/Taxonomy/CognitiveMemoryTaxonomyEntities.cs`
- `src/CanDoItAll.Modules.CognitiveMemory/Taxonomy/CognitiveMemoryTaxonomyEntityConfigurations.cs`
- `src/CanDoItAll.Modules.CognitiveMemory/Taxonomy/CognitiveMemoryTaxonomyServices.cs`
- `src/CanDoItAll.Modules.CognitiveMemory/Foundation/CognitiveMemoryEntities.cs`
- `src/CanDoItAll.Modules.CognitiveMemory/Foundation/CognitiveMemoryEntityConfigurations.cs`
- `src/CanDoItAll.Migrations.Sqlite/Migrations/20260516190839_AddCognitiveMemoryTaxonomyAndProjections.cs`
- `src/CanDoItAll.Migrations.PostgreSql/Migrations/20260516191004_AddCognitiveMemoryTaxonomyAndProjections.cs`
- `tests/CanDoItAll.Tests.Unit/CognitiveMemoryTaxonomyTests.cs`
- `tests/CanDoItAll.Tests.Integration/CognitiveMemoryTaxonomyPersistenceModelTests.cs`
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter FullyQualifiedName~CognitiveMemoryTaxonomyTests` passed 6/6.
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter FullyQualifiedName~CognitiveMemoryTaxonomyPersistenceModelTests` passed 2/2.
- `dotnet test .\tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-build --filter CognitiveMemory` passed 49/49.
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter CognitiveMemory` passed 12/12.
- SQLite/PostgreSQL pending-model checks reported no changes.
- `dotnet build .\CanDoItAll.slnx --no-restore` passed with zero warnings.

## Browser Validation Logging

- No browser proof is required unless projection health UI is included.
- Health UI evidence belongs to `08-human-review-ui`.

## Progression Gate

- Passed for workspace/attention. Recall remains blocked until workspace/attention and prediction-error/salience foundations close.

## Suggested Agent Prompt

- Implement the durable memory taxonomy and projection lifecycle while preserving source truth and projection rebuildability.
