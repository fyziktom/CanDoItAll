# SB02 — Publication, source, import, audit persistence and state model

State: `DONE`
Proof tier: `Governed`  
Depends on: `SB01`  
Next on pass: `SB03`

## Objective

Add explicit PostgreSQL-backed Workspace ownership for publication, source, import, stable source identity, invocation metadata, concurrency, and deterministic reconciliation.

## Observable outcome

The relational model can safely represent central publication and client imports without JSON-only identity, token duplication, destructive outage behavior, or local ID churn.

## Inputs and current-state anchors

- Bundle root execution contract and architecture documents.
- Current repository state, not only the prepared SHA.
- Relevant source/test impact maps.
- Completed proof and handoff from every dependency.
- Current mandatory SharedInfo skills.

## Scope

- Add cohesive Workspace SharedProviders entity/configuration files.
- Implement ProviderSharePublication with separate stable public ID.
- Implement SharedProviderSource with canonical URI, one secret reference, trusted-network policy, source identity, ETag, status, and concurrency.
- Implement SharedProviderImport with unique source/publication identity, stable local ProviderProfile link, sanitized snapshot, selection and availability state.
- Reuse a canonical existing installation identity or add a stable SharedProviderServiceIdentity row.
- Implement SharedProviderInvocationRecord metadata/usage shape and retention-ready timestamps.
- Add PostgreSQL migration and model snapshot.
- Implement publication/source/import repositories or application services using IDbContextFactory and current transaction/concurrency conventions.
- Implement pure reconciliation state machine and transaction coordinator without network calls.
- Define source edit propagation/effective-profile materialization invariant chosen in SB00/SB01.
- Integrate provider deletion/reference checks and commit observers.
- Do not expose these entities as API DTOs.

## Out of scope

- No remote catalog HTTP.
- No public API routes.
- No upstream inference.
- No Razor UI.
- No direct SQL fixtures.

## Implementation sequence

1. Split entities, EF configurations, enums/value mappings, repositories/services, and reconciliation into cohesive top-level files.
2. Use indexes and FK delete behavior explicitly.
3. Use application-managed concurrency tokens consistent with the current context.
4. Make successful-authoritative-catalog absence distinct from transient sync failure.
5. Preserve ProviderProfile.Id across unpublish/missing/reappearance.
6. Preserve local alias/enabled intent while remote-owned fields update.
7. Add audit record finalization idempotency/concurrency.
8. Generate migration only after the model is stable; inspect generated SQL/snapshot.
9. Add a clean PostgreSQL migration integration test.

## C# Architecture Impact

This subbundle is architecture-significant. Re-read
`architecture/00-csharp-current-state-inventory.md` through
`architecture/04-csharp-testability-plan.md`, update the affected checkpoint, and stop rather
than use a boundary workaround.

## Boundary Ownership

Workspace owns EF/application state. Foundation only discovers entity configurations and hosts migrations; it does not reference Workspace.

## Dependency Direction

Workspace references SharedProviders Abstractions. No reference to SharedProviders.Http. Migrations follow existing module model registry.

Record before and after `ProjectReference`/namespace direction even when no reference is
expected to change. A no-change result is still evidence.

## Pattern Decision

Aggregate/application service with explicit relational entities, state machine, optimistic concurrency, transactional outbox-free synchronous commit observers as current conventions permit.

Do not introduce an adjacent alternative pattern without reopening the owning ADR and
recording why the selected pattern failed.

## Testability Contract

Pure transition tests plus PostgreSQL mapping/migration/transaction tests. Fixed clock and GUID source where current infrastructure supports it.

Every new behavior needs one realistic positive proof and one meaningful negative proof. Test
existence, file counts, status codes alone, or mocked self-assertions do not prove behavior.

## Partial Class Policy

Do not append entities/service methods to WorkspaceModels.cs. New SharedProviders folder and top-level service classes are required.

A large partial or monolithic file is a gate failure unless the architecture review documents
a narrow unavoidable reason.

## Architecture Proof Required

- Entity/index/FK inventory.
- Migration and snapshot diff.
- Clean database migrate-up proof.
- Concurrency conflict behavior.
- State transition table positive/negative tests.
- Stable local profile ID and alias/enabled invariants.
- No plaintext token or content columns.
- Provider deletion/reference behavior.

## Test selection

| Topic | Owning project/lane | Stable filter | Planned expected discovery | Selection reason |
| --- | --- | --- | ---: | --- |
| `SharedProviderStateModelTests` | `tests/Solutions/CanDoItAll.Tests.Unit.slnx` | `FullyQualifiedName~SharedProviderStateModelTests` | 18 | Covers publication/source/import transitions and invariants. |
| `SharedProviderPersistenceIntegrationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderPersistenceIntegrationTests` | 14 | Covers EF mappings, indexes, transactions, concurrency and clean PostgreSQL migration. |
| `SharedProviderDeletionReferenceIntegrationTests` | `tests/Solutions/CanDoItAll.Tests.Integration.slnx` | `FullyQualifiedName~SharedProviderDeletionReferenceIntegrationTests` | 6 | Prevents orphaned publication/import/local profile identity. |

Before running a test topic:

1. build the owning production/test assembly;
2. run `--list-tests` when it is a .NET test lane;
3. compare actual discovery with the planned count;
4. update the planned count only before execution and with a written implementation-based
   reason;
5. reject zero discovery;
6. record transcript and counts in `proof/proof-manifest.json`.

Do not run an unfiltered project or broader lane unless this subbundle explicitly owns it.

## Acceptance criteria

- All explicit identities/relationships are relational and unique.
- Source owns one credential reference.
- Transient failure cannot mark all imports missing.
- Authoritative unpublish/missing preserves local profile identity.
- Migration works on clean PostgreSQL.
- No large partial/monolith growth.

## Negative proof

- Duplicate source/publication import rejected.
- Publication public ID cannot equal/expose provider ID by mapping.
- Source identity mismatch transition blocks reconciliation.
- Concurrent update produces typed conflict.
- Audit schema cannot persist request/response body fields.
- Direct delete cannot orphan a referenced import/publication.

## Semantic invariants

- Workspace remains canonical provider/share/source/import owner.
- Secret values and content are absent from the relational schema.
- Local provider identity survives remote lifecycle changes.

## Evidence artifacts

At minimum:

- completed `proof/proof-manifest.json`;
- command transcripts under `proof/transcripts/`;
- changed-file inventory;
- architecture/reference artifacts;
- focused behavior artifacts;
- completed `SESSION-HANDOFF.md`;
- updated root `STATUS.md` and traceability rows.

## Progression gate

Pass only when every acceptance criterion, architecture assertion, focused build/test, and
negative proof is backed by an artifact. On pass mark this subbundle `DONE`, unlock only
`SB03`, and update the owning review.

On failure, keep downstream work locked. Do not call a missing proof a residual risk.

## Reopen triggers

- Current provider deletion model has a different owner.
- Existing installation identity is unsuitable.
- Model registry/migration convention changed.
- Effective-profile materialization cannot preserve source ownership without a different relation.

## Downstream revalidation overlay

SB04 later changed invocation operation/image-usage persistence, the existing migration/model
snapshot, and additive usage ABI. That named invalidation was revalidated without rewriting this
subbundle's original PASS history or frozen 18/14/6 counts. The exact reruns, sandbox-only deletion
failure chronology, EF no-pending-model result, hash comparison, and migration amendment assumption
are recorded in
[`proof/architecture/sb04-downstream-invalidation-revalidation.md`](proof/architecture/sb04-downstream-invalidation-revalidation.md).

## Execution checklist

- [x] Current branch/commit/worktree captured.
- [x] Mandatory skills loaded.
- [x] Bundle and subbundle readiness validated.
- [x] Dependencies are `DONE`.
- [x] Before architecture/reference evidence captured.
- [x] Scope implemented without widening.
- [x] Affected production projects built.
- [x] Test discovery recorded and nonzero.
- [x] Focused positive/negative tests passed.
- [x] Security/redaction checks passed where applicable.
- [x] After architecture/reference evidence captured.
- [x] Proof manifest completed with artifact hashes.
- [x] Session handoff completed.
- [x] Status/traceability/review updated.
