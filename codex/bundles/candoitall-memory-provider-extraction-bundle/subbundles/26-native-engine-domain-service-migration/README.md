# 26 Native Engine Domain Service Migration

## Status

- `Completed`

## Objective

- Move native recall, consolidation, quality, scoring, taxonomy, signals, temporal replay, procedural, workspace, operations, and review application services into native service projects.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R15
- R16

## Prerequisites

- SB25 completed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Recall/CognitiveMemoryRecallServices.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Quality/CognitiveMemoryDreamConsolidationService.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Consolidation/CognitiveMemoryConsolidationServices.cs`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Migrate native recall, ingestion, consolidation, quality, scoring, taxonomy, temporal replay, review, probing, professor, and self-regulation domain/application services into the native repo.
- Split overgrown native services during migration rather than preserving large mixed files from the original module.
- Replace host service dependencies with native abstractions, protocol adapters, source gateway client calls, or explicit external dependency ports.
- Keep optional semantic/RAG/Qdrant projection behind `Projection.Rag` and configuration guards.
- Add parity tests for key current native recall/consolidation/probing/review behaviors using native DbContext and mock dependencies.

## Dependency Impact

- Native protocol API and remote provider behavior depend on migrated services.

## Validation Depth

- `Native engine migration`

## Implementation Steps

1. Use the inventory to migrate native folders in dependency order: Foundation, Common, Recall, Ingestion, Consolidation, Quality, Scoring, Signals, Advanced, UI-facing application services.
2. For each moved service, classify dependencies as native-owned, generic protocol/source gateway, optional external driver, or obsolete host coupling.
3. Refactor files that combine domain, persistence, UI DTOs, and orchestration into smaller domain/application/persistence pieces.
4. Add compatibility tests comparing representative current-module behavior to native-service behavior where feasible.
5. Document intentional behavior changes and any deferred advanced native feature migration.

## Scope Exceptions

- No known scope exceptions for this subbundle at preparation time.
- If implementation discovers an exception, document it in `reviews/01-execution-report.md` and stop before downstream work if the exception affects a phase gate.

## Do Not Do

- Do not implement downstream subbundles early.
- Do not introduce direct generic-memory or MAF references to native Cognitive Memory implementation types.
- Do not add Qdrant as a base runtime dependency.
- Do not expose host EF entities or DbContext instances to memory providers.
- Do not duplicate memory operation dispatch logic outside the shared handler.

## Acceptance Checklist

- The implemented surface is observable through focused tests or explicit proof artifacts.
- Dependency boundaries from `requirements/03-non-negotiable-boundaries.md` remain intact.
- No downstream subbundle work is silently implemented or assumed.
- Execution report is updated with proof paths, command transcripts, and gate result.
- Native engine code builds and runs in the native repo without the main CanDoItAll module.
- Optional Qdrant/RAG paths are isolated behind projection interfaces and not required for basic native service startup.
- Representative recall, ingestion, consolidation, quality, and professor/probe paths have parity or documented migration tests.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB26/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB26/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB26/manifest.md` and `proof/SB26/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run native-service build/test commands from the `CanDoItAll.CognitiveMemory` repository after confirming real target paths, and capture transcript paths in the manifest.
- Run native application/domain test suite and selected parity tests against migrated services.
- Run source audit for accidental references to main app module, main AppDbContext, and direct source module internals.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB26 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB26 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
