# SB04 Semantic Invariants

## SB04-INV-001

Expected behavior: Add explicit workflow/subprocess artifact adapters and source-run versioning.

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
