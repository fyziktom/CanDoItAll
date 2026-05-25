# SB07 Semantic Invariants

## Invariant

Expected behavior: Make recovery continuation work consistently for direct agent, workflow-backed, subprocess-backed, and manager-recovered artifacts.

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
