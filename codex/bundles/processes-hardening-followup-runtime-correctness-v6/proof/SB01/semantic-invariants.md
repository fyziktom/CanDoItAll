# SB01 Semantic Invariants

## Invariant SB01-INV-001

Expected behavior: Fix writable/read-only alias overlap and make prompt-grounding merge safe.

Disallowed shallow implementation:
- prompt-only change
- source-assertion-only proof
- tests that do not exercise production code path
- branch-specific hardcoding
- software-only behavior in generic process runtime
- adding more fragile text heuristics without typed state

Required proof:
- failing-first or red-team test
- passing behavior test
- source assertions
- anti-stub audit
- changed-file hashes
