# SB04: Refactor metadata and grounding logic after SB01-SB03.

## Objective

Refactor metadata and grounding logic after SB01-SB03.

## Why This Matters

This subbundle closes a concrete runtime correctness gap observed after phase5. The process runtime must avoid both false completion and unnecessary blocking while staying generic.

## Implementation Tasks

- Extract `ProcessStepOperationContractResolver`.
- Extract `ProcessTargetGroundingLedgerBuilder`.
- Extract `ProcessInvocationMetadataBuilder`.
- Move tests from reflection-heavy calls toward direct unit tests for extracted services.
- Update architecture documentation and source assertions.

## Required Tests

- Add failing-first or red-team tests before the production fix where practical.
- Add positive tests proving the fixed behavior.
- Include at least one generic/non-software case if this subbundle changes generic process semantics.

## Closure Criteria

- Production code implements the behavior; no prompt-only fix.
- Proof manifest is updated.
- Focused tests pass.
- No SQLite runtime/migration dependency is introduced.
