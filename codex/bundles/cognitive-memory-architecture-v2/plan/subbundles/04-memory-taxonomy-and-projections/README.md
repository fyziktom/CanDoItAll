# 04 Memory Taxonomy And Projections

## Status

- Ready after source ingestion and adapters.

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

- Every memory record points to source evidence.
- Projection records are deleteable and rebuildable.
- Relation confidence and source evidence are explicit.
- Projection similarity is stored as a projection signal dimension, not as final memory rank.

## Proof Required

- Taxonomy invariant tests.
- Projection rebuild tests.
- Golden dataset proving context-separated relatedness.

## Browser Validation Logging

- No browser proof is required unless projection health UI is included.
- Health UI evidence belongs to `08-human-review-ui`.

## Progression Gate

- Proceed to recall only after canonical memory, projection lifecycle, and score-geometry-backed activation/relation signals are trustworthy.

## Suggested Agent Prompt

- Implement the durable memory taxonomy and projection lifecycle while preserving source truth and projection rebuildability.
