# 04 Source Snapshot And Ingestion Contracts

## Status

- `Completed`

## Objective

- Define Source Gateway abstractions, source snapshot DTOs, source request model, provenance, sensitivity, redaction, source module identity, and ingestion job contracts.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R07
- R08

## Prerequisites

- SB01 completed

## Exact Source References

- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Ingestion/CognitiveMemorySourceIngestionContracts.cs`
- `repo://src/Modules/CanDoItAll.Modules.CognitiveMemory/Ingestion/CognitiveMemorySourceIngestionService.cs`
- `repo://src/Memory/CanDoItAll.Memory.SourceGateway.Abstractions/MemorySourceSnapshotModels.cs`
- `repo://src/Modules/CanDoItAll.Modules.Workbench/ProjectStructure/WorkbenchProjectStructureSourceSnapshotProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/WorkflowRuntimeEvidenceSourceProvider.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/UnavailableProcessRuntimeEvidenceSourceProvider.cs`
- `bundle://architecture/04-runtime-operations-and-feedback.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Define Source Gateway interfaces for host modules to expose source snapshots without exposing EF entities or DbContext instances.
- Define source snapshot envelopes with source module id, source record ids, source version/hash, provenance, sensitivity classification, redaction policy, and requested source scope.
- Define provider-initiated source request contracts and user-initiated ingestion job contracts.
- Define source payload forms for structured JSON facts, text sections, file references, artifact refs, and future binary/link references.
- Add validation for allowed source scopes and fail-closed behavior when source permission or adapter is missing.
- Reuse, rehome, or explicitly migrate the existing `MemorySourceSnapshot*` contracts in `CanDoItAll.AgentFramework.Core`; do not create a parallel source snapshot contract family.
- Preserve current source snapshot semantics for provenance, redaction, sensitivity, cursor/hash/version, source identity, and unavailable-source diagnostics.

## Dependency Impact

- All source adapter and ingestion subbundles depend on these contracts to avoid AppDbContext leakage.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Create source gateway abstractions before any concrete project/process/CRM adapter is implemented.
2. Define adapter registration and discovery contracts with module id, supported source kinds, permission requirements, and snapshot version support.
3. Create DTOs for manual ingestion actions, provider source requests, source snapshot responses, and ingestion job status.
4. Add unit tests using fake source adapters to prove provenance, redaction, sensitivity, missing adapter, and denied permission behavior.
5. Document how source snapshots are passed to providers and how source ids are stored in operation/feedback ledgers.
6. Add a source-contract migration decision note to the proof manifest: reused in place, moved with namespace compatibility, or temporarily adapted with explicit retirement criteria.

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
- No provider-facing source contract exposes EF entities, `AppDbContext`, lazy-loaded navigation properties, or module-internal service types.
- Provider-initiated source requests and user-initiated `Ingest into memory` actions share the same source snapshot model.
- Source snapshots include enough provenance to support citations, later feedback, audit, and redaction review.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB04/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB04/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB04/manifest.md` and `proof/SB04/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run fake Source Gateway tests for successful snapshot, missing adapter, denied source scope, redacted field, and unsupported source kind.
- Run source audit proving provider drivers cannot directly reference host module DbContexts.
- Run a source audit proving there is no second incompatible `MemorySourceSnapshot*` or source snapshot DTO family unless a migration adapter and tests are present.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB04 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB04 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
