# SB06 Semantic Invariants

## SB06-INV-001

Expected behavior: Prevent branch routing from masking missing artifacts on artifact-production steps while preserving review/approval disposition routing.

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
