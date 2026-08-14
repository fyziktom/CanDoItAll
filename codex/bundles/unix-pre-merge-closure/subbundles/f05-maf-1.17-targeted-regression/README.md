# MAF 1.17 authority and approval regression gate

## Goal

Bind the final candidate to the already implemented MAF 1.17 behavior without reopening the wrapper architecture.

## Entry

Read the root execution prompt, findings, requirements, invariants and
validation strategy. Reconfirm the exact repository anchor before editing.

## Tasks

1. Verify exact stable/preview package baselines.
2. Run package reflection tests.
3. Run approval session round-trip and continuation tests.
4. Run runtime architecture service tests.
5. Run canonical authority and activity coordinator tests.
6. Run execution-run tracking integration tests.
7. Investigate only actual failures; do not perform speculative MAF refactoring.

## Rules

- Preserve unrelated changes.
- Use focused failing-first tests.
- Keep source comments in English.
- Do not push or merge.
- Do not weaken a validator to make evidence pass.
