# Implementation Prompt

You are implementing `process-dispatch-artifact-validation-rule-boundary-v1` in `maf-processes-refactor`.

Rules:

- Execute subbundles in order.
- Stop at Gate A, Gate B, Gate C, and Final Gate for source scans and tests.
- Do not create Process Core or driver packs.
- Do not move EF entities, UI, storage implementations, or MAF composition.
- Preserve all artifact validation semantics exactly.
- Add failing-first tests/source scans before production movement where possible.
- Keep comments in source code in English.
- Do not run mobile/small/medium viewport proof. Runtime/service proof is N/A unless UI files unexpectedly change.
