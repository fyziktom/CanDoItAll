# 05 Foundation Refactoring Checkpoint

## Status

- `Completed`

## Objective

- Refactor and harden SB01-SB04 foundations before runtime implementation starts.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R01
- R02
- R03
- R04
- R05
- R06
- R07
- R20

## Prerequisites

- SB01-SB04 completed

## Exact Source References

- `bundle://plan/02-checkpoints.md`
- `bundle://requirements/03-non-negotiable-boundaries.md`
- `bundle://analysis/02-assumptions-and-risks.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Review all code added in SB01-SB04 for contract bloat, native leakage, duplicated validators, inconsistent id types, weak serialization tests, and hidden downstream assumptions.
- Extract shared validation helpers, id value objects, capability constants, and serialization helpers into small focused files.
- Add architecture guard tests for generic-contract dependency direction and native/Qdrant absence.
- Tighten public APIs and remove temporary TODOs that would make runtime implementation ambiguous.
- Update protocol/source/ledger docs and reopen SB01-SB04 if a semantic flaw is found.
- Verify the foundation uses the current MAF provider/source snapshot contracts and has not forked provider dispatch or source snapshot models.
- Verify zero-provider startup and operation-denial semantics are specified before runtime services are implemented.

## Dependency Impact

- Blocks all runtime work if contracts are native-specific, duplicated, untested, or semantically shallow.

## Validation Depth

- `Critical checkpoint`

## Implementation Steps

1. Run a source audit across new generic memory projects for `CognitiveMemory`, `Qdrant`, `AppDbContext`, and native module namespaces.
2. Review file sizes and split overgrown contracts or helper classes before runtime services are added.
3. Verify all protocol, provider, ledger, and source snapshot records have validation and negative tests.
4. Verify every new production state or event has producer/consumer/lifecycle documentation in proof.
5. Fix or reopen the owning foundation subbundle; do not patch around a weak contract in the checkpoint.
6. Search for duplicate source snapshot families and implicit fallback providers; block SB06 if either exists.

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
- SB01-SB04 foundations are small enough to maintain and have no `misc` helper bucket.
- Dependency guards fail if generic memory contracts reference native Cognitive Memory or Qdrant.
- Downstream runtime subbundles can implement against stable contracts without inventing missing fields.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB05/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB05/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB05/manifest.md` and `proof/SB05/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Capture source-audit output for forbidden dependencies and overgrown files.
- Capture a checklist showing whether SB01-SB04 were closed, fixed, or reopened.
- Capture current MAF/source snapshot compatibility proof and zero-provider no-dispatch proof.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB05 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB05 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
