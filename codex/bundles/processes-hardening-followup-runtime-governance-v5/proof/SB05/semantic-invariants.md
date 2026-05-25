# SB05 Semantic Invariants

## Invariant

Expected behavior: Use stable typed lineage identity for artifact dedupe and audit rather than bounded ExternalReferenceKey.

Disallowed shallow implementation:
- prompt-only change
- source-assertion-only proof
- tests that do not exercise production code path
- branch-specific hardcoding
- software-only behavior in generic process runtime

Required proof:
- failing-first or red-team test
- passing behavior test
- source assertions
- anti-stub audit
- changed-file hashes
