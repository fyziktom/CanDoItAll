# Shared effect invariants

P02-SHARED: application producers emit immutable change scope from authoritative persistence results; consumers never reconstruct affected IDs from UI selection. New/local/unaffected drafts retain their identity, EditContext, raw text and section. Affected imported projections reload, retired/missing/malformed targets fail closed without choosing another provider. Unknown scope refreshes metadata and marks stale state without destructive editor replacement.

P02-LIFETIME: target and overlay cancellation plus generation checks own every await continuation, busy clear, notification and callback. No global CloseAll. Shared target A cannot provide mutation arguments while B is displayed. Source overlay close cancels owner-cancellable requests; known backend commits survive secondary observer/read/cancellation failures independently of UI publication.

P02-PUBLICATION: Sharing read is side-effect free; first explicit Publish atomically creates permanent identity with publication. Before identity, local deletion is allowed. Existing and unpublished identities remain permanent and guarded. No audit deletion or public ID reuse.

P02-AUTHORITY: registry ownership precedes revision comparison and connector effects. Imported alias/enabled intent and retirement are local; source endpoint/credential/trust belong to the source; remote models/capabilities belong to catalog reconciliation. Runtime materialization is an effective projection, not another writable authority.

The source finding numbers, all 31 exact direct tests, and real PostgreSQL production producers/consumers are mapped in bundle://proof/SBC/topic-map.json and architecture/04-csharp-testability-plan.md. Failing-first evidence is the two valid A-to-B load cases plus read-creates-identity regression. The original disposal fixture did not invoke lifecycle and is excluded as defect evidence. Corrected public disposal tests pass and prove cancellation through the component's public seam. Before/after source hashes and exact portable transcripts are supplied by the final SBC manifest.

Disallowed shallow implementations: fake success receipts proving a registry transaction; untyped Changed causing full Load; UI-only ownership guards; swallowing observer exceptions as rollback; preserving target data while letting an orphan overlay publish; read-time publication creation; replacing failed imports with the first available provider.

Red-team cases: late A success and failure, pending mutation after B, source close/dispose during save/test/sync, unknown remote scope, permanent unpublished identity, source credential changes resetting trust, forged local connector on a persisted import. Downstream proof: production API tests, the parent provider workspace rendering tests and full-app browser retention/retirement/publication scenarios.
