# 29 Native Service Hardening Checkpoint

## Status

- `Completed`

## Objective

- Refactor and harden native service projects, DB ownership, API contracts, MAF abstraction usage, UI surface package, workers, and optional Qdrant projection.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R14
- R15
- R16
- R20

## Prerequisites

- SB24-SB28 completed

## Exact Source References

- `bundle://plan/02-checkpoints.md`
- `bundle://architecture/06-native-service-extraction.md`
- `bundle://inventories/02-dependency-and-removal-inventory.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Audit SB24-SB28 native service for DB ownership, project dependency direction, optional Qdrant isolation, API contract completeness, worker behavior, native MAF policy, and overgrown migrated files.
- Refactor migrated native services into maintainable domain/application/persistence/API/UI/worker boundaries before host decoupling starts.
- Add dependency guards for native repo and main repo boundary assumptions.
- Run native service startup, health, protocol API, DB migration, worker, and optional projection tests.
- Block host dependency removal until the native service can operate as a real optional provider.
- Prove the native service is an explicit provider endpoint/configuration path and not an implicit fallback used by base startup.

## Dependency Impact

- Blocks main host dependency removal if native service still depends on host internals or lacks protocol proof.

## Validation Depth

- `Critical checkpoint`

## Implementation Steps

1. Run source audits for host AppDbContext references, main Agent module references, base host module references, and mandatory Qdrant usage in native service startup.
2. Inspect file sizes and split migrated large services/helpers that would make future native development fragile.
3. Verify native protocol API covers query, ingestion, feedback, status, health, capability, and event flows.
4. Verify native workers and MAF integration use policy, cancellation, and loop guards.
5. Record native checkpoint result and reopen SB24-SB28 if optional-provider invariants fail.
6. Run main-host zero-provider startup proof before allowing SB30 to remove old references.

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
- Native service owns its DB and engine and can run independently as an optional memory provider.
- Native service startup does not require the main app or Qdrant unless optional projection is enabled.
- Host decoupling can safely switch from in-process/native module references to provider-driver configuration.
- Main-host zero-provider behavior remains typed and does not silently call the native service.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB29/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB29/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB29/manifest.md` and `proof/SB29/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Run native-service build/test commands from the `CanDoItAll.CognitiveMemory` repository after confirming real target paths, and capture transcript paths in the manifest.
- Capture native dependency audit, startup proof, DB migration proof, and API contract proof.
- Run native service test suite and selected main-repo driver integration tests after refactoring.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Completion Summary

- Native provider events now persist in native-owned EF storage, flow through native MAF services, and are delivered through `/memory/events`.
- Native service startup registers native persistence plus native MAF services without host module, host app, or Qdrant base dependencies.
- Native worker startup registers native persistence and observes pending provider events through a typed worker pulse.
- Main-host zero-provider runtime and component tests passed before SB30 host composition removal begins.
- Closure proof is recorded in `bundle://proof/SB29/manifest.md` and `bundle://proof/SB29/semantic-invariants.md`.

## Progression Gate

- Downstream subbundles may start only after SB29 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB29 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
