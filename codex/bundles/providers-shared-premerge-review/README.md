# Shared Providers Pre-Merge Review and Repairs

Review: 2026-08-30. Profile: feedback. Product: main CanDoItAll repository.
Branch/head: `providers-shared` / `3fc10d2db7ba7e4e15bc94f50e66f815f31c4219`.
Base: `development` and local `origin/development`, both `1625b336e4f60ddb64987240c3a3dc485591d20f`.

**Repair and verify before merge.** This delivery contains a review and implementation plan, not production fixes, published schemas, or a merge.

## Validation Summary

- Bundle preparation status: `Prepared`
- Bundle readiness gate: `Pass — structural and independent semantic reviews`
- Execution status: `Not started`
- Subbundle gate review: `Prepared; execution gates pending`
- Final closure gate: `Not started`

Start with [prioritized findings](analysis/03-prioritized-review.md), [execution plan](plan/01-phase-plan.md), and [validation selection](plan/02-validation-strategy.md).
Evidence: [providers](analysis/provider-review.md), [performance](analysis/performance-review.md), [documentation/contracts](analysis/docs-contracts-review.md), [CodeAnalytics](analysis/codeanalytics-summary.json), [synthetic redaction reproduction](analysis/redaction-reproduction.json).
[Requirements](requirements/01-normalized-requirements.md) and [traceability](traceability/01-requirement-traceability.md) map every requested area to an owner.

| Unit | Outcome | State | Proof tier |
| --- | --- | --- | --- |
| [SB01](subbundles/01-relay-completion-contract/README.md) | Correct buffered, streaming, oversized-error responses | Ready | Behavioral |
| [SB02](subbundles/02-source-network-policy/README.md) | Discovery/import/runtime agree on loopback policy | Ready | Behavioral |
| [SB03](subbundles/03-history-capture-integrity/README.md) | Redaction and truthful timeout outcomes | Ready | Behavioral |
| [SB04](subbundles/04-history-retention/README.md) | Reclaim expired orphan input details safely | Ready | Behavioral |
| [SB05](subbundles/05-provider-hotpaths/README.md) | Reduce verified repeated hot-path work | Pending SB01/SB02/SB04 | Behavioral |
| [SB06](subbundles/06-openapi-contracts/README.md) | Accurate machine-readable protocol contract | Pending SB01/SB02 | Behavioral |
| [SB07](subbundles/07-product-documentation/README.md) | Maintained guides and six project READMEs | Pending final behavior | Standard |
| [SB08](subbundles/08-sharedinfo-and-schema-export/README.md) | Final OpenAPI, API skills, PostgreSQL review SQL | Pending contract/doc freeze | Behavioral |
| [SB09](subbundles/09-premerge-proof-and-handoff/README.md) | Current-source proof and historical closure | Pending all units | Governed |

Preserve existing ProviderManagement / SharedProviders / ProviderHistory boundaries. No blanket sealing, LINQ replacement, SDK upgrade, new runtime partial, or generic provider framework. No new project is required by the verified repairs.

SharedInfo is read-only during preparation. SB08 owns future reusable source-package edits there; installed synchronization uses its installer and applicable write approval. Product migrations remain the database source of truth. “New scheme” primarily means OpenAPI, with separate PostgreSQL migration/export verification.

Original shared-providers SB07 three-application proof and Docker budget restrictions remain in force. This bundle does not reset budgets or claim later two-instance tests closed that gate. See [handoff rules](plan/03-historical-handoff.md).

[Readiness review](reviews/00-bundle-self-review.md) records preparation validation. Future repair/merge proof belongs in [execution report](reviews/01-execution-report.md). The user merges manually.
