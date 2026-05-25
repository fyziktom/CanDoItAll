# SB03 Semantic Invariants

## SB03-INV-001

Expected behavior: Fix recovery lineage so manager recovery artifacts validate against the recovery execution that produced them and the original execution they recover for.

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
