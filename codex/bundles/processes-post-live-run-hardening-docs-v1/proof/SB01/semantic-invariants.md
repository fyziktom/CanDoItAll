# SB01 Semantic Invariants

## Invariants

- Invariant ID: SB01-INV-001
- Source raw note: RN01 - Audit latest successful run evidence, recent local bundle reports, and remaining proof debt.
- Expected behavior: The audit preserves every proof-debt item from the reviewed state and assigns it to a downstream subbundle or an explicit blocker.
- Disallowed shallow implementation: Declaring previous blockers solved only because a successful live run was reported.
- Failing-first test: N/A - process/non-production audit; local bundle inventory transcript shows the prior process/preflight bundles are absent and cannot be used as closure proof.
- Passing test: bundle://proof/SB01/proof-debt-audit.md classifies each debt item and keeps open debts assigned.
- Changed source files: bundle-only files under bundle://README.md, bundle://inputs/ and bundle://subbundles/.
- Production assertions: No production code changed in SB01; production behavior remains untouched until later subbundles.
- Red-team negative case: Missing prior bundle artifacts are classified as locally unavailable instead of treated as proof.
- Downstream dependency check: SB02 can proceed because every proof-debt category has an owner or current blocker status.
