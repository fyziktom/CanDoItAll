# SB05 governed proof manifest

State: `PASS`.

## Outcome

SB05 implements the client source and import pipeline without a new project edge: neutral catalog
transport contracts; typed/redacted credentials; safe DNS-revalidating HTTP; source CRUD/test/
enable/disable/reset; identity pinning; ETag synchronization; deterministic selection/reconciliation;
stable profiles; and post-commit observer notification.

The work ran on `providers-shared` at unchanged commit
`e46f81d5ee33627dccb548732725e1c37e980ab5`. The pre-existing uncommitted SB00-SB04 work was
preserved. No commit, staging, discard, push, or unrelated-file overwrite occurred.

## Architecture

Before snapshot `snap-20260825051057-300644c7` and final force-refreshed snapshot
`snap-20260825070408-300644c7` cover the same 14 product projects. The final graph has 758 source
documents, 35 modules, 5,231 dependency facts, 34 direct references, zero project cycles, the
unchanged governed two module/one nested-type cycles, and zero error findings. The exact reference
comparison reports no delta.

Workspace owns source/import use cases and transactions behind Abstractions. Http owns safe
transport and references only Abstractions. Composition owns concrete wiring. No partial class was
introduced. `architecture/changed-namespace-public-surface-review.md` and
`architecture/cross-review.md` record the detailed review and repaired findings.

## Authoritative proof

| Gate | Result | Artifact |
| --- | --- | --- |
| Unit Release build | 0 warnings/errors; 37.065 s | `transcripts/sb05-build-unit-release-closure-final.txt` |
| Integration Release build | 0 warnings/errors; 36.721 s | `transcripts/sb05-build-integration-release-deselection-corrected-final.txt` |
| URI/network policy | 18 discovered; 18 passed | `transcripts/sb05-list-source-uri-policy-release-closure-final.txt`; `transcripts/sb05-run-source-uri-policy-release-closure-final.txt` |
| reconciliation | 22 discovered; 22 passed | `transcripts/sb05-list-reconciliation-release-review-fixes.txt`; `transcripts/sb05-run-reconciliation-release-review-fixes.txt` |
| real source sync | 16 discovered; 16 passed | `transcripts/sb05-list-source-sync-integration-release-deselection-final.txt`; `transcripts/sb05-run-source-sync-integration-release-deselection-final.txt` |
| SB04 relay invalidation | 24 discovered; 24 passed | `transcripts/sb05-revalidate-run-sb04-relay-policy-release.txt` |
| SB01/SB02 invalidation | 12/12 protocol, 18/18 state, 14/14 PostgreSQL persistence | `transcripts/sb05-revalidate-run-sb01-protocol-release.txt`; `transcripts/sb05-revalidate-run-sb02-state-release.txt`; `transcripts/sb05-revalidate-run-sb02-persistence-release.txt` |
| anti-stub/no-partial | pass for 14 production files | `transcripts/sb05-anti-stub-audit-review-fixes.txt`; `transcripts/sb05-no-partial-audit-review-fixes.txt` |
| secret/content/log containment | pass | `transcripts/sb05-persistence-content-secret-field-audit-review-fixes.txt`; `transcripts/sb05-production-log-call-audit-review-fixes.txt` |
| diff hygiene | exit 0 | `transcripts/sb05-diff-check-closure-final.txt` |
| closure validator | pass | `transcripts/sb05-closure-validator-final.txt` |

The historical `sb05-failing-first-unit-build.txt` is a test-fixture constructor compile failure,
not a semantic red, and is retained only for honest chronology. Semantic confidence comes from the
adversarial exact positive/negative lanes and independent cross-review; no stronger failing-first
claim is made.

## Behavior and negative proof

The exact lanes prove canonical reverse-proxy paths, explicit TLS/private policy, per-connection DNS
classification, rebinding protection, no redirects/proxy/cookies, actual registered handlers, URI-log
suppression, bounded catalog validation, one source credential for multiple imports, idempotency,
stable IDs/local intent, replacement de-selection retirement without row deletion, authoritative
missing, reappearance, identity mismatch/reset,
source edits, disabled short-circuit, post-enable sync, and post-commit observers.

Userinfo/query/fragment/non-HTTP(S), public plain HTTP, unsafe/private destinations without approval,
special-purpose addresses, mixed DNS answers, malformed/oversized/duplicate catalog data, invalid
schema/scope/identity, stale concurrency, transient/auth/404/upstream failure, and stale ETag recovery
all fail explicitly and non-destructively. A 304 cannot mask unhealthy source/import state.

## Security

Only an existing secret-record ID is persisted. Default HttpClient loggers are removed for catalog
and relay named clients, request/token stringification is redacted, application logs contain only
sanitized status metadata, and persistence contains no authorization, token value, prompt, request
body, or response body. Redirects/proxy/cookies are disabled, DNS is revalidated on every connection,
and platform TLS validation remains intact.

## Progression

All SB05 acceptance, architecture, test, and security gates pass. SB06 alone is unlocked. Runtime
connector projection/no-fallback remains SB06; multi-instance proof remains SB07; UI remains
SB08/SB09; the broad aggregate and running-stack closure remain SB12.
