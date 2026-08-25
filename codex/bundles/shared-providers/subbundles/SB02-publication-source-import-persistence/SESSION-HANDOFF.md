# SB02 session handoff

State: `COMPLETE`

## Outcome

SB02 passed CP-02. Workspace owns explicit PostgreSQL-backed publication, source, import, stable
service identity, and invocation metadata plus deterministic reconciliation, source/profile
materialization, typed deletion/transfer safety, and truthful shared-relay usage classification.
Only SB03 may proceed.

## Current repository state

- branch: `providers-shared`
- commit before: `e46f81d5ee33627dccb548732725e1c37e980ab5`
- commit after: `e46f81d5ee33627dccb548732725e1c37e980ab5` (no commit created)
- working tree before: completed SB00/SB01 plus readiness-repair changes, captured at SB02 entry
- working tree after: uncommitted SB00-SB02 source, tests, and Governed proof; see
  `proof/transcripts/sb02-working-tree-final.txt`
- unrelated changes preserved: no pre-existing unrelated change was staged, committed, discarded,
  or overwritten

## Changed files

- 24 cohesive Workspace SharedProviders entity/configuration/state/service files;
- generated `20260824224847_AddSharedProviderPersistence` migration/designer and model snapshot;
- Workspace composition/model/delete/transfer integration and one Infrastructure conflict helper;
- appended AgentFramework Usage relay classifications;
- exactly three focused test classes and SB02/root proof/architecture/traceability artifacts.

The complete inventory is `proof/changed-files.md`; after-state hashes are
`proof/hashes.sha256`.

## Architecture evidence

- checkpoint: `PASS_SB02`
- ProjectReference before: `proof/architecture/project-references-before.md`
- ProjectReference after: `proof/architecture/project-references-after.md`
- CodeAnalytics before: `snap-20260824213007-c65710b4`
- CodeAnalytics after: `snap-20260824231242-d9fc36b9`
- graph: 12 projects, 25 direct product references, zero project cycles; only new edge is
  `Workspace -> SharedProviders.Abstractions`; baseline module/type cycles unchanged
- public surface: all 36 new Workspace declarations, Usage enum additions, and the Infrastructure
  helper reviewed in `proof/architecture/changed-namespace-public-surface-review.md`
- partial classes: none created/extended; largest handwritten SB02 production file is 217 lines
- independent review: `PASS`, no remaining blocker

## Build and focused test evidence

| Topic | Expected | Actual | Passed | Failed | Skipped | Artifact |
| --- | ---: | ---: | ---: | ---: | ---: | --- |
| `SharedProviderStateModelTests` | 18 | 18 | 18 | 0 | 0 | `proof/transcripts/sb02-run-state-release.txt` |
| `SharedProviderPersistenceIntegrationTests` | 14 | 14 | 14 | 0 | 0 | `proof/transcripts/sb02-run-persistence-release.txt` |
| `SharedProviderDeletionReferenceIntegrationTests` | 6 | 6 | 6 | 0 | 0 | `proof/transcripts/sb02-run-deletion-release.txt` |

Workspace, Unit, and Integration Release builds report zero warnings/errors. The final persistence
lane uses real PostgreSQL and includes a two-import propagation assertion plus persisted stale-
token rollback. EF reports no pending model changes.

## Positive behavior

- Five tables, 13 indexes, five restrictive FKs, explicit stable identities, and typed application
  concurrency are generated and migrate on a clean database.
- Reconciliation preserves import/profile ID, alias, enabled intent, trusted source identity, and
  state through repeated sync, outage, authoritative absence, mismatch, and reappearance.
- Source URI/secret-reference edits update two linked profiles in one transaction, and observers
  see committed profiles only.
- Invocation begin/finalize is owner-consistent, idempotent, metadata-only, retention-ready, and
  truthful about incomplete usage/pricing.

## Negative behavior

- Invalid/equal identities, duplicate source/publication import, wrong service identity, stale
  token, owner mismatch, and invalid state transitions fail explicitly.
- Both production delete surfaces, PostgreSQL `Restrict`, and destructive transfer preflight
  prevent orphaning or partial mutation.
- Anti-stub and secret/content/schema scans pass.

## Security and redaction

Only secret-record IDs are stored. No upstream secret value, Authorization header, private
endpoint payload, prompt, response, image, attachment, tool argument, or raw upstream content is
persisted. Cached remote JSON is a bounded versioned sanitized envelope.

## Remaining risks and downstream constraints

- SB03 owns publication eligibility, explicit administrator publish, sanitized catalog, auth,
  models route, and ETag/304.
- SB05 owns real source networking, SSRF/DNS/redirect policy, and conditional sync.
- SB06 must preserve fail-closed imported profiles when registering the shared connector.
- SB08 must install server-side remote-field ownership before enabling generic editing.
- SB04/SB12 own relay population and invocation-retention cleanup.
- The two baseline module cycles and one nested-type cycle remain unchanged repository debt.

These are assigned downstream constraints, not missing SB02 proof.

## Reopen triggers observed

None. Reopen on model/migration drift, provider delete/transfer owner change, materialization
mechanism change, public identity/constraint change, usage enum renumbering, or dependency-graph
change.

## Progression decision

- result: `PASS`
- next subbundle: `SB03`
- reason: architecture, migration, real persistence/concurrency, exact focused tests,
  deletion/transfer negatives, public-surface review, and containment proof all pass

## Downstream restored-trust addendum

SB04's invocation operation, image-usage, migration, model-snapshot, and additive ABI changes
triggered a scoped downstream invalidation of SB02 proof. The original CP-02 result and its
transcripts remain unchanged. Fresh exact reruns restored trust at 18/18 state, 14/14 persistence,
and 6/6 deletion/reference tests; EF reports no pending model changes. The first restricted-sandbox
deletion attempt is retained as an `UnauthorizedAccessException` bootstrap failure, followed by
the passing approved rerun of the identical command. Full chronology, hashes, evidence links, and
the never-applied-migration assumption are in
[`proof/architecture/sb04-downstream-invalidation-revalidation.md`](proof/architecture/sb04-downstream-invalidation-revalidation.md).
