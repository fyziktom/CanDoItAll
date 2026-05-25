# SB10 Semantic Invariants

## SB10-INV-001

Expected behavior: process definitions with high or mission-critical criticality, or guarded/delegated autonomy, are automatically linted in strict mode during publish and run start even when callers do not explicitly request strict lint. The process editor must show every lint issue, and strict lint must remain generic instead of treating every non-software report/review process as product mutation.

Disallowed shallow implementation:

- prompt-only change
- source-assertion-only proof
- tests that manually seed final state instead of exercising producer/consumer lifecycle
- branch-specific hardcoding
- software-only behavior for generic process runtime

Required proof:

- failing-first/red-team proof
- passing proof
- source assertions
- anti-stub audit
- changed-file hashes
- production behavior artifact matrix when new runtime state is introduced

Implemented proof:

- Publish and run-start paths derive strict lint from persisted process risk metadata before accepting risky definitions.
- Strict lint blocks product-mutation steps that lack a typed operation contract, but accepts architecture/reporting work that does not mutate product targets.
- The lint panel renders the complete issue collection instead of hiding issues after the fourth entry.
- Focused service, linter, component, and browser smoke tests cover the production paths and editor surface.
