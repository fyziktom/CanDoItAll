# Revision 2 — changes to the previous handoff

Reviewed on **2026-09-23** against the then-current `development` branch and the supplied original ZIP. The new package supersedes that ZIP; do not execute two competing task documents.

## Material revisions

| Area | Revision and implementation consequence |
|---|---|
| Starting branch | The owner creates the feature branch before execution. All operational instructions use that checked-out branch and its current source. Historical commit equality, ancestry checks and automatic branch switching are explicitly excluded. Actual starting/ending commits are report metadata only. |
| Further development drift | Re-discover current files, migrations, APIs, tests and dependencies at execution. Complete work already present instead of duplicating it. The review is not permission to roll back intervening changes. |
| Components / CI | Retain the newer branch-aware dependency resolver: PR base branch, otherwise workflow ref; resolve Components once per run and reuse its commit. Do not restore the previous fixed Components pin or introduce a same-name feature-branch requirement for a PR into development. FileTools remains separately pinned under the current policy. |
| Test infrastructure | Preserve the 60-second DB-maintenance budget, 15-second normal query budget, fixture pool cleanup, WAL_LOG selection policy, fixture-only HTTP-handler-lifetime fix, Linux container init requirement, current stable budgets/exclusions and portability enforcement. Use current asynchronous component test helpers. |
| Provider data | Reconcile image-input/cached-image-input prices, null versus zero, custom configuration, historical prices and sharing/vault identities. Public catalogs negotiate fields through a feature header and cannot substitute for full private-state migration. |
| Durable execution data | Preserve version 1 and compressed version 2 protocol envelopes, package/type fingerprints and long journals. Validate read-back and safe decoding without reissuing historical side effects or weakening recovery guards. |
| Runtime/profile changes | Preserve Simple Chats profile-lease serialization and SSE cancellation fixes. Verify the effective database after host restart, not via stale connections or a merely saved profile. |
| API fallback | Discover current HTTPS/auth behavior, including Swagger-specific redirects. Verify actual routes and per-item results. No invented database migration endpoint, public projection presented as a backup, auth bypass or partially copied target activation. |
| Evidence | Added an advisory validation map and more precise report fields. Audit references are navigation aids, not required versions; all runtime outcomes remain unexecuted until the coding agent proves them. |

## What remains deliberately unchanged

The upgrade is still a single end-to-end long task, not workflow bundles. Keep the new-installation baseline on a verified stable PostgreSQL 18.x release, update both database installer and generated launcher, and use PostgreSQL 18's correct Docker volume layout. Do not build an automatic cross-major conversion framework. Old installations receive a safe refusal plus an agent runbook prominently linked from the root README and tools index.

Preserving the real workstation application on web port **5032** remains mandatory. The ordinary developer option to start over with disposable test data is not permission to reset that instance. Prefer a full dump/restore into an isolated target, preserve matching files/keys and prove existing content plus effective runtime binding after restart. Seriously investigate the supported API fallback when direct access cannot be made safe; retain the source when completeness cannot be proved.

## Review limits

The source comparison contains eight commits since the previous review. This review followed that delta and fetched the important current database, launcher, CI, test, API and persisted-protocol surfaces. It did not execute a build, inspect the owner's workstation, download replacement binary artifacts, run CI, invoke the local API or migrate live data. The instructions therefore require those verifications at execution and never label them as already passed.

See [current evidence and sources](references/RECHECK_AND_SOURCES.md), [validation map](references/VALIDATION_MAP.md), and [audit metadata](references/REVIEW_METADATA.json). The metadata's commit IDs explain what was inspected; no file in this package uses them to constrain the implementation branch.
