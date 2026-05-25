# SB03 Semantic Invariants

## Invariant

Expected behavior: Replace broad text-scraped external alias grants with a typed grounded-target ledger.

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
