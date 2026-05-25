# SB07 Semantic Invariants

## SB07-INV-001

Expected behavior: Make artifact validation storage-backed, explicit-mode friendly, and less brittle for generic processes.

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
