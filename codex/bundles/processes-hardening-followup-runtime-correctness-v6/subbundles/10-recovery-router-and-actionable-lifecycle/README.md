# SB10: Make recovery options executable and deterministic.

## Objective

Make recovery options executable and deterministic.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add `ProcessRecoveryRouter`.
- Given block code, failure ownership, and diagnostics, select next recovery action.
- Persist recovery routing events and next-action state.
- Prevent repeated no-progress recovery attempts without new evidence.
- Add tests for wait-for-materialization, recover-artifacts-only, fresh-agent-session, human-escalation, repair-implementation.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
