# SB11: Refactor recovery, block state, and health diagnostics.

## Objective

Refactor recovery, block state, and health diagnostics.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Extract `ProcessBlockStateClassifier`.
- Extract `ProcessRecoveryRouter` if not already isolated.
- Extract `ProcessHealthInvariantAuditor`.
- Extract `WorkflowSubprocessArtifactMapper`.
- Ensure no single process dispatch partial class grows with new recovery logic.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
