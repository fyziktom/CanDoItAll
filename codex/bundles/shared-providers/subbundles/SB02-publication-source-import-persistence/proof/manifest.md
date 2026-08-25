# SB02 governed proof manifest

State: `PASS`

## Baseline and implementation

SB02 started and completed on branch `providers-shared` at commit
`e46f81d5ee33627dccb548732725e1c37e980ab5`; no commit, staging, discard, or unrelated-file
rewrite occurred. Workspace now owns five explicit relational entities, focused state
transitions/services, deterministic reconciliation, one stable service identity, invocation
metadata, provider-reference enforcement, and one generated PostgreSQL migration.

No remote HTTP, endpoint, SDK relay, connector registration, or Razor UI was added.

## Owned requirements and raw notes

| Scope | SB02 result | Portable evidence |
| --- | --- | --- |
| FR-003 | stable public publication ID is relational, unique, and database-checked as distinct from the internal profile ID | `bundle://subbundles/SB02-publication-source-import-persistence/proof/architecture/entity-index-fk-inventory.md` |
| FR-025, FR-029 | source shape and unique source/publication import identity are relational; source owns one secret reference | `bundle://subbundles/SB02-publication-source-import-persistence/proof/architecture/migration-snapshot-diff.md` |
| FR-030, FR-031, FR-033 through FR-035 | reconciliation is idempotent/non-destructive and preserves local profile ID, alias, enabled intent, and trusted source identity | `bundle://subbundles/SB02-publication-source-import-persistence/proof/behavior/persistence-and-reconciliation.md` |
| FR-038 through FR-040 | deletion/transfer references are fail-closed; source edits update all linked caches; only existing secret-record IDs are stored | `bundle://subbundles/SB02-publication-source-import-persistence/proof/architecture/persistence-decision-lock.md` |
| FR-050 through FR-053 | metadata-only invocation shape and truthful completeness exist; relay population and retention cleanup remain downstream | `bundle://subbundles/SB02-publication-source-import-persistence/proof/security/persistence-containment.md` |
| NFR-012, NFR-018 through NFR-020, NFR-028, NFR-029, NFR-031 | optimistic conflicts, canonical Workspace ownership, relational state, cohesive files, transactional consistency, outage safety, and database-backed identities pass | `bundle://subbundles/SB02-publication-source-import-persistence/proof/semantic-invariants.md` |
| NFR-013, NFR-033, NFR-034, NFR-036, NFR-037 | retention-ready content-free schema and exact deterministic focused proof pass; cleanup remains downstream and the broad gate remains reserved | this manifest and `bundle://subbundles/SB02-publication-source-import-persistence/proof/proof-manifest.json` |

Raw authority is preserved in `bundle://inputs/00-user-request-verbatim.md`, the SB00/SB01
handoffs, and `bundle://architecture/05-target-domain-and-data-model.md`. SB02 did not reinterpret
network/SSRF, HTTP sync, relay, connector runtime, editor ownership, or UI notes as in-scope work.

## Failing-first record

The state and persistence test sources initially failed against the incomplete implementation
because their required entity/service types did not exist. The deletion selection also exited
red during the incomplete parallel implementation because the reconciliation coordinator did not
compile; this is recorded as a truthful build-stage red, not overstated as behavioral deletion
proof. Final positive and meaningful negative behavior comes from the exact green suites and real
PostgreSQL constraints.

## Producer, consumer, and lifecycle matrix

| Production artifact | Producer | Current consumer | Lifecycle / downstream proof |
| --- | --- | --- | --- |
| publication row and public ID | `repo://src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderPublicationService.cs` | invocation ownership and deletion policy; SB03 catalog next | row survives unpublish; concurrent creation converges; profile delete is blocked |
| source row and derived profile cache | `repo://src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderSourceService.cs` | reconciliation and linked `ProviderProfile` runtime cache | two-profile propagation is atomic; stale update is rejected with persisted rollback state |
| import/profile identity | `repo://src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderReconciliationCoordinator.cs` | deletion policy and future SB06 runtime projection | repeated sync/missing/reappearance preserve IDs and local intent |
| stable service identity | `repo://src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderServiceIdentityStore.cs` | SB03 catalog projection next | database singleton survives concurrent hosts/restarts |
| invocation metadata | `repo://src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderInvocationAuditService.cs` | SB04 relay and existing usage projection downstream | begin/finalize are idempotent; owner-consistent; retention indexed; no content |
| provider-reference policy | `repo://src/Modules/CanDoItAll.Modules.Workspace/SharedProviders/SharedProviderProfileDeletionPolicy.cs` | both Workspace and AgentFramework delete paths plus transfer preflight | typed block before mutation; database `Restrict` remains authoritative |

The portable invariant contract is
`bundle://subbundles/SB02-publication-source-import-persistence/proof/semantic-invariants.md`.
Before/after hashes are in `bundle://subbundles/SB02-publication-source-import-persistence/proof/changed-files.md`
and `bundle://subbundles/SB02-publication-source-import-persistence/proof/hashes.sha256`.

## Critical Foundation downstream proof

Infrastructure produces
`repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/SerializableMutationScope.cs#IsUniqueConstraintConflict`.
The internal Workspace conflict classifier consumes it for publication and reconciliation named
constraints. The real 14-test PostgreSQL lane proves concurrent publication convergence and
actual duplicate import rejection/translation. The exact public-surface and dependency review is
`bundle://subbundles/SB02-publication-source-import-persistence/proof/architecture/changed-namespace-public-surface-review.md`.

## Commands and durable evidence

| Gate | Result | Artifact |
| --- | --- | --- |
| Entry validator | Pass | `transcripts/sb02-entry-validator.txt` |
| Workspace Release build | 0 warnings/errors | `transcripts/sb02-build-workspace-release.txt` |
| Unit Release build | 0 warnings/errors | `transcripts/sb02-build-unit-release.txt` |
| Integration Release build | 0 warnings/errors | `transcripts/sb02-build-integration-release.txt` |
| State list/run | 18 discovered, 18 passed | `transcripts/sb02-list-state-release.txt`; `sb02-run-state-release.txt` |
| Persistence list/run | 14 discovered, 14 passed | `transcripts/sb02-list-persistence-release.txt`; `sb02-run-persistence-release.txt` |
| Deletion list/run | 6 discovered, 6 passed | `transcripts/sb02-list-deletion-release.txt`; `sb02-run-deletion-release.txt` |
| EF pending model | no pending changes | `transcripts/sb02-ef-pending-model-release.txt` |
| Anti-stub | Pass, 31 selected files | `transcripts/sb02-anti-stub-audit.txt` |
| Credential/content schema | Pass | `transcripts/sb02-secret-content-scan.txt` |
| Diff whitespace | Pass | `transcripts/sb02-diff-check.txt` |

## SB04 downstream invalidation and restored trust

The table above is the original SB02 closure record. SB04 subsequently changed invocation
operation/image-usage persistence, the migration/model snapshot, and additive usage ABI, so the
affected SB02 evidence was treated as invalid until fresh validation completed. The unchanged
frozen filters again discovered and passed 18/18 state, 14/14 persistence, and 6/6
deletion/reference tests; EF reported no pending model changes.

The first sandboxed deletion rerun failed before assertions because access to the user-local
control-plane lock was denied. The identical approved rerun passed 6/6. Both transcripts remain in
chronological order. The durable hash comparison, exact transcript links, restored-trust decision,
and the assumption that the amended migration has never reached a durable/non-disposable database
are in
`bundle://subbundles/SB02-publication-source-import-persistence/proof/architecture/sb04-downstream-invalidation-revalidation.md`.

## Architecture evidence

The force-refreshed comparison is
`snap-20260824213007-c65710b4 -> snap-20260824231242-d9fc36b9`: 12 projects remain, direct
product references move 24 to 25, and project cycles remain zero. The only new product edge is
`Workspace -> SharedProviders.Abstractions`. Foundation/Migrations discover model configuration
without referencing Workspace. The two baseline module cycles and one nested-type cycle are
unchanged.

The independent C# reviewer returned `PASS` after repairs for publication races, composite audit
ownership, real duplicate rejection, clean-migration coverage, transfer preflight, versioned
snapshots, and exact proof language. No partial class was added; `WorkspaceModels.cs` gained only
the existing-path deletion policy call, while all feature types live in cohesive top-level files.

## Progression

SB02 passes CP-02. SB03 alone may proceed with eligibility, explicit administrator publication,
sanitized catalog/auth/ETag API behavior. Remote HTTP, inference relay, source networking, runtime
connector registration, and UI remain downstream-owned. The one broad test gate remains reserved
for SB12.
