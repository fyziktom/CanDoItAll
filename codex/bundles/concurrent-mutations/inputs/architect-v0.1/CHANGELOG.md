# Changelog

## 0.1.0 · 2026-09-28

Initial architecture/discovery package following the review of stale human/agent writes.

Added sampled evidence with immutable source anchors and explicit coverage gaps; separate target/basis/authority semantics; revision and write/lock policy; outcome and owner-receipt design; UI conflict versus postcommit reconciliation; agent approval/takeover rules; projections/external effects; lifecycle/compatibility; nine gated phases; performance experiments, risk and test traceability; read-only evidence-refresh and local package-validation tools.

Important qualifications retained: Project Save explicitly releases its mutation scope before its postcommit projection; the source-authorized PostgreSQL path differs from the helper's relational striped-lock path; existing task and UI-seam protections are preserved; runtime creation receipts do not themselves provide durable operation replay; no application test/benchmark or exhaustive caller audit was performed.

Status remains proposed. Source and branch references are evidence, not implementation checkout requirements. No repository changes, implementation commits, deployment or migrations were made by this package.
