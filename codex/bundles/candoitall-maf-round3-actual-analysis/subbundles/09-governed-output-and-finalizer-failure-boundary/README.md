# 09 - Governed Output and Finalizer Failure Boundary

## Problem

Round 2 improved governed structured/finalizer output, but production behavior must be protected by behavior tests.

## Required rules

- Required finalizer missing: retry/fail, never complete.
- Required finalizer invalid: retry/fail, never complete.
- Structured output invalid after repair budget: retry/fail, never complete.
- Post-finalizer mutation tool: finalizer sequence violation.
- Missing required branch outcome: retry/block/fail, never complete from prose.

## Acceptance criteria

- Behavior tests cover all rules.
- Process mutation tools are included in sequence-significant tool classification.
- Completion reason records structured-output/finalizer failure category.
- Exhausted retry budget creates Failed/Blocked step with structured failure details.

## Execution status

Completed. Process mutation tools are sequence-significant, post-finalizer mutations violate validation, and retry/failure metadata is represented through typed recovery decisions and ledger entries.
