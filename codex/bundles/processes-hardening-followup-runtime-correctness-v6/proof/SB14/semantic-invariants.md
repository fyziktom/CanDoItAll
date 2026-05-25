# SB14 Semantic Invariants

## Invariant SB14-INV-001

Expected behavior: Run generic software and non-software red-team scenarios.

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
