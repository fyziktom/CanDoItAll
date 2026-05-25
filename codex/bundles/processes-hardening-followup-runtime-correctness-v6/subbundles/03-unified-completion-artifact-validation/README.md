# SB03: Use finalizer-grade artifact validation for manual/API transitions.

## Objective

Use finalizer-grade artifact validation for manual/API transitions.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Extract finalizer artifact validation into a reusable service or static validator.
- Replace `TransitionStepAsync` local `ValidateRequiredArtifactsForCompletion` with the shared validator.
- Ensure manual transitions reject placeholder, malformed JSON, stale/wrong-run, wrong producer, and missing storage content.
- Ensure exception/repair branch policy is typed, not only branch-title text.
- Add tests for manual completion with malformed JSON and placeholder artifact.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
