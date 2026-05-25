# SB12: Define strict vs compatibility contract modes.

## Objective

Define strict vs compatibility contract modes.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add contract strictness policy for process definition versions.
- New or edited definitions should require explicit operation contract for risky steps.
- Legacy/migrated definitions can run in compatibility mode with visible warnings.
- Allow strict mode to be enforced on publish/run-start by criticality/autonomy.
- Add migration/template update tests.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
