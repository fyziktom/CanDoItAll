# SB09 Semantic Invariants

## SB09-INV-001

Expected behavior: Integrate process definition lint into publish/start/readiness and improve templates.

Disallowed shallow implementation:

- prompt-only change
- source-assertion-only proof
- tests that do not exercise production code path
- branch-specific hardcoding
- generic process behavior that only works for software delivery

Required proof:

- failing-first or red-team test
- passing behavior test
- source assertions
- anti-stub audit
- changed-file hashes
