# SB02 Semantic Invariants

## SB02-INV-001

Expected behavior: Make tool policy enforce process boundaries for external targets and managed output product paths, and prevent prompt alias auto-promotion.

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
