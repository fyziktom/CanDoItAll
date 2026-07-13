# 10 Generic Runtime Refactoring Checkpoint

## Status

- `Completed`

## Objective

- Refactor and harden generic runtime, drivers, workers, diagnostics, and tests before source adapters are added.

## Success Criteria

- The subbundle outcome is implemented behind the intended boundary and does not leak downstream responsibilities.
- Positive and negative proof exercise production code paths, not only hand-built DTOs or stubs.
- Downstream phases can rely on the produced contracts/runtime behavior without guessing or compensating for missing seams.

## Covered Inputs

- R03
- R04
- R05
- R06
- R17
- R20

## Prerequisites

- SB06-SB09 completed

## Exact Source References

- `bundle://plan/02-checkpoints.md`
- `bundle://inventories/02-dependency-and-removal-inventory.md`
- `bundle://architecture/07-testing-and-mocking-strategy.md`
- `bundle://analysis/04-live-repo-reentry-alignment.md`
- `bundle://requirements/01-normalized-requirements.md`
- `bundle://plan/01-phase-plan.md`

## Deliverables

- Review SB06-SB09 runtime code for duplicated dispatch, overgrown service classes, inconsistent status transitions, hidden synchronous waits, native/Qdrant leakage, and weak diagnostics.
- Extract helper services for status transitions, driver dispatch, retry policy, operation lease, feedback retention, and event routing where files grew too large.
- Add architecture guards for generic runtime dependency direction and base startup without provider configuration.
- Strengthen tests that only assert non-empty results so they verify real producer/consumer lifecycle behavior.
- Update runtime docs and phase gate notes before Source Gateway adapters start using the runtime.
- Prove zero-provider runtime behavior stops at typed selection/dispatch results and does not invoke native Cognitive Memory, Qdrant, OpenAI, or mock providers implicitly.

## Dependency Impact

- Blocks source adapters and MAF integration if runtime has native/Qdrant leakage or weak async proof.

## Validation Depth

- `Critical checkpoint`

## Implementation Steps

1. Run source audits for forbidden references and duplicate operation handler patterns.
2. Inspect async methods for `.Result`, `.Wait()`, blocking sleeps, missing cancellation token propagation, and long-lived DbContext use.
3. Split overgrown workers/services into bounded collaborators with focused tests.
4. Verify operation, event, and feedback ledgers have producer/consumer lifecycle tests, not only repository tests.
5. Record the checkpoint result and reopen SB06-SB09 if a runtime invariant is not met.

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
- No generic runtime project references native Cognitive Memory or Qdrant.
- No memory operation path bypasses the generic operation ledger/status lifecycle.
- Generic runtime registration succeeds with zero provider profiles and no hidden fallback provider.
- Runtime helper boundaries are clear enough for source adapters, MAF tools, and UI to depend on them safely.
- Semantic proof would fail against a stub, renamed old implementation, in-memory-only shortcut, or test-only manually seeded signal.

## Proof Required

- Create `proof/SB10/manifest.md` with changed-file hashes, failing-first transcript, passing transcript, source assertions, and anti-stub audit output.
- Create `proof/SB10/semantic-invariants.md` covering raw-note closure, shipped behavior, shallow-pass trap, adversarial negative proof, semantic positive proof, and downstream dependency check.
- Add a `Production Behavior Artifact Matrix` in `proof/SB10/manifest.md` and `proof/SB10/semantic-invariants.md` for every new state, event, ledger record, worker signal, or provider-visible behavior introduced here.
- Run `dotnet build CanDoItAll.slnx` unless the subbundle README documents a narrower build gate with justification.
- Run focused unit tests, integration tests, or architecture guard tests that directly exercise this subbundle, not only broad happy-path smoke tests.
- Capture dependency audit output, async misuse audit output, and file-size/helper-split review results.
- Run runtime tests after refactoring and record before/after changed-file hashes.

## Browser Validation Logging

- N/A. This subbundle has no browser-visible surface. Record N/A in the execution report unless implementation touches a host-visible or browser-visible surface.

## Progression Gate

- Downstream subbundles may start only after SB10 proof is recorded, the acceptance checklist passes, and no phase-gate blocker remains.

## Suggested Agent Prompt

```text
Implement subbundle SB10 only. Start by reading this README and the Exact Source References. Preserve the generic memory boundaries, avoid downstream work, capture the required proof, update reviews/01-execution-report.md, and stop if the progression gate cannot pass honestly.
```
