# SB09: Replace heuristic workflow/subprocess artifact matching with explicit mapping.

## Objective

Replace heuristic workflow/subprocess artifact matching with explicit mapping.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Add process definition mapping from workflow artifact output id/name/kind to process artifact expectation id.
- Add subprocess parent mapping from child process artifact expectation id to parent expectation id.
- Block ambiguous mapping instead of guessing by kind/title/summary.
- Keep legacy heuristic as warning-only compatibility fallback.
- Add tests with multiple same-kind artifacts where heuristic would choose the wrong artifact.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
