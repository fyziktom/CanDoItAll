# SB09 Semantic Invariants

## Invariants

- `SB09-INV-001`: Completed bundle proof must include a manifest and semantic-invariants file for every subbundle.
- `SB09-INV-002`: The old V1 SB08 fake/fixture proof must fail the new proof-quality checker.
- `SB09-INV-003`: The new SB04 process E2E proof must pass the same checker.
- `SB09-INV-004`: At least one missing-operation-contract adversarial case must pass.
- `SB09-INV-005`: At least one unknown/command tool adversarial case must pass.
- `SB09-INV-006`: Provider usage reconciliation must report unresolved external billing rows explicitly rather than hide them.
- `SB09-INV-007`: UI/browser proof must be manually reviewed for misleading usage/cost and blocker state.

## Evidence

- `bundle://proof/SB09/transcripts/prepared-validation.txt`
- `bundle://proof/SB09/transcripts/proof-quality-old-v1-expected-failure.txt`
- `bundle://proof/SB09/transcripts/proof-quality-new-sb04-pass.txt`
- `bundle://proof/SB09/transcripts/adversarial-contract-and-tool-policy.txt`
- `bundle://proof/SB09/billing-reconciliation-review.md`
- `bundle://proof/SB09/browser-analytics.md`
- `bundle://proof/SB09/final-red-team-report.md`

## Residual Risk

Existing EF package version warnings remain in build/test logs. They are outside this bundle's behavioral scope and should be handled as dependency hygiene.
