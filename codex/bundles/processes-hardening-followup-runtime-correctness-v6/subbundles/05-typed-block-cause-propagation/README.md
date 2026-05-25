# SB05: Replace reason-text block inference with typed causes.

## Objective

Replace reason-text block inference with typed causes.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add typed block cause to `ProcessStepTransitionRequest` or a parallel transition metadata object.
- Make finalizer pass `OwnOutput`, `UpstreamInput`, `RuntimeEvidence`, or `PolicyDenied` cause explicitly.
- Fix `ProcessStepRunBlockState.InferBlockReasonCode` fallback so own required artifact failure is not classified as missing upstream artifact.
- Add tests for own missing artifact vs upstream missing artifact.
- Ensure recovery options differ correctly for own-output artifact recovery and upstream materialization.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
