# SB09 Semantic Invariants

## SB09-INV-001

Expected behavior: see `subbundles/09-durable-no-progress-ledger-and-active-run-reconciliation/README.md`.

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
