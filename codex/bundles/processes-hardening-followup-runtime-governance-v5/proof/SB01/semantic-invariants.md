# SB01 Semantic Invariants

## Invariant

Expected behavior: Add first-class persisted operation-contract fields to process step definitions, editor models, import/export, templates, and UI.

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
