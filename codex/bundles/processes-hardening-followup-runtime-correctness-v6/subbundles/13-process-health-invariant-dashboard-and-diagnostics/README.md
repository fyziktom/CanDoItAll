# SB13: Expose runtime invariant violations and actionable diagnostics.

## Objective

Expose runtime invariant violations and actionable diagnostics.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add view models and service methods for alias conflicts, weak artifact records, blocked recovery state, duplicate lineage identity, and manual transition validation failures.
- Display process health and recommended action.
- Ensure UI remains generic across process types.
- Add component tests if UI changes.
- Add journal events for invariant audit results.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
