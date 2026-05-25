# SB14: Run generic software and non-software red-team scenarios.

## Objective

Run generic software and non-software red-team scenarios.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add scenario harness cases: architecture-only software step, business plan external artifact destination, legal approval, manufacturing QA, incident response, workflow-backed role, subprocess parent, manager recovery.
- Validate no architecture/planning step mutates product targets.
- Validate manual/API completion cannot bypass artifact validation.
- Validate recovery router selects correct next action.
- Run final full validation and completed-stage bundle validator.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
