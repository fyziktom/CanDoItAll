# SB01 Semantic Invariants

## SB01-INV-001

Expected behavior: Add an explicit generic step operation contract and harden the classifier so artifact production is not confused with product mutation.

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
