# 14 Neuro Foundation Claim Evidence Ledger

## Status

- Ready after `01a-common-drivers-helpers-and-ef-guardrails` and `01b-score-geometry-driver`.
- Critical foundation.

## Objective

Add the architecture and implementation plan for atomic claims, evidence anchors, entity/context binding, and memory mutation authority before any downstream phase creates durable memory semantics.

## Covered Inputs

- Neuro patch FR-041 through FR-044 and NFR-025, NFR-026, NFR-032.
- Patch findings C-02, C-03, H-02, M-01, M-02, and M-03.
- Existing v2 source truth, provenance, review, EF, and Qdrant projection constraints.

## Prerequisites

- `00-prerequisite-boundary-gate` has confirmed source snapshot and MAF context contribution boundaries.
- `01-module-foundation` has established the module/persistence boundary.
- `01a-common-drivers-helpers-and-ef-guardrails` has shared ids, fakes, paging, serialization, and EF query-shape helpers.
- `01b-score-geometry-driver` provides belief-state score spaces, vector snapshots, shapes, scalar projection policy, and evaluation traces.

## Exact Source References

- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\17-neuro-cognitive-integration-layer.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\20-claim-evidence-belief-ledger.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\21-schema-entity-context-binding.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\03-memory-taxonomy-and-data-model.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\architecture\10-security-governance-and-provenance.md
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\contracts\csharp\CognitiveMemory.NeuroPatchContracts.cs
- C:\repositories\CanDoItAll\codex\bundles\cognitive-memory-architecture-v2\validation\test-and-quality-plan.md

## Deliverables

- Evidence anchor records and contracts.
- Claim/evidence/belief records and support/attack relation model.
- Entity registry, alias, context frame, and context boundary model.
- Mutation authority command/result/audit design.
- Query/index rules for claims, anchors, contexts, belief score components, mutation commands, and audit events.
- Projection payload enrichment rules for claim ids, context frame ids, and belief state.

## Dependency Impact

- Source ingestion must create evidence anchors and context hints instead of only broad source refs.
- Taxonomy/projection must treat memory items as claim containers rather than sole truth units.
- Recall must rank and render claim-level candidates where available.
- Probing corrections must create claim mutation candidates rather than direct memory updates.
- Consolidation, learning, replay, and distributed acceptance must submit authoritative changes through mutation authority.

## Validation Depth

- EF model/index tests for anchors, claims, support/attack links, context frames, aliases, mutation commands, and audit rows.
- Unit tests for claim support/attack/belief-state calculation.
- Score geometry tests proving belief state is not support-minus-attack scalar arithmetic.
- Negative tests for generated-summary promotion without evidence anchors.
- Mutation idempotency, stale version token, review-required, and audit-event tests.
- Docker context-boundary test proving production/test/local/CI contexts are related but not substitutable.
- Performance/EF review for claim/evidence query shape before source ingestion starts.

## Implementation Steps

1. Add evidence anchor and claim/belief contracts/entities/configurations.
2. Add entity/context binding contracts/entities/configurations.
3. Add mutation command/result/audit contracts/entities/configurations.
4. Add query DTOs and paging contracts for claims, anchors, context frames, and mutation audits.
5. Add projection payload validation rules for claim/context/belief metadata.
6. Add tests and the Docker context-boundary fixture.
7. Run architecture review before allowing source ingestion or recall phases.

## Scope Exceptions

- Do not build recall ranking, workspace routing, probing UI, replay scheduling, or answer gating in this subbundle.
- Do not implement full natural-language entity extraction beyond deterministic/testable binding seams needed by downstream phases.

## Do Not Do

- Do not expose public direct upsert operations for authoritative memory.
- Do not store query-relevant claim/evidence/context state only in JSON.
- Do not let evidence anchors replace raw source records.
- Do not silently merge claims with different context frames, validity windows, or evidence state.
- Do not let Qdrant payloads become the authoritative claim store.

## Acceptance Checklist

- Claims can be represented below memory items.
- Evidence anchors are fine-grained and source-versioned.
- Belief state can differ between claims in the same memory item.
- Context frames prevent unsafe semantic substitution.
- Mutation authority is the public write boundary.
- Public contracts avoid ordinal `MinimumValidationState` semantics and untyped projection payloads.
- Belief state persists score vector/evaluation evidence, not only support and attack totals.

## Proof Required

- Build/test output for new entities/contracts where implementation happens.
- EF model/index proof.
- Mutation authority idempotency/audit proof.
- Docker context-boundary fixture output.
- Implementation report with deviations and reopened assumptions.

## Browser Validation Logging

- N/A for this backend/domain foundation.
- Browser proof is required later in review, recall trace, probing, and answer-gate UI phases that expose claims and evidence anchors.

## Progression Gate

- Do not proceed to source ingestion, taxonomy/projection, recall, consolidation, probing, learning, cross-project, or distributed phases until claim/evidence/context/mutation authority contracts and tests pass.
- Reopen this subbundle if any downstream phase needs direct authoritative upsert, JSON-only claim lookup, context-insensitive semantic merge, or scalar-only belief scoring.

## Suggested Agent Prompt

Implement the neuro-cognitive foundation for Cognitive Memory: evidence anchors, atomic claims, belief state, entity/context binding, and mutation authority. Preserve raw source truth, keep Qdrant a projection, and make authoritative writes command-based, idempotent, audited, and review-aware.
