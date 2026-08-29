# SB03 — History Storage And Lifecycle

## Status

- Execution: Completed

## Objective

- Implement bounded metadata/detail/policy persistence and durable lifecycle primitives without changing canonical transcript ownership or enabling cleanup before source deletion proof.

## Covered Inputs

- N006, N008–N011; R006, R008–R011, R014.
- [Normalized requirements](../../requirements/01-normalized-requirements.md).

## Prerequisites

- SB01 identity/boundary gate passed; coordinate pricing fields with SB02.
- Identify actual AppDbContext configuration and PostgreSQL migration registration paths.
- Disposable PostgreSQL/file fixtures available; no user database or destructive migration is authorized.

## Exact Source References

- `repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs`
- `repo://src/App/CanDoItAll.Composition/ModuleAssemblies.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationAuditService.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/SharedProviderPersistenceIntegrationTests.cs`
- `repo://tests/Integration/CanDoItAll.Tests.Integration/DatabaseMigrationIntegrationTests.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatWholeUseCaseProfileScopeTests.cs`
- `bundle://architecture/01-csharp-boundary-map.md`
- `bundle://architecture/05-history-data-lifecycle.md`
- `bundle://architecture/09-search-security-contract.md`
- `bundle://architecture/10-pricing-and-capture-contract.md`

Linked source context:

[EF model registry](C:/repositories/CanDoItAll/src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContextModelRegistry.cs).
[Composition module assemblies](C:/repositories/CanDoItAll/src/App/CanDoItAll.Composition/ModuleAssemblies.cs).
[Relay audit service](C:/repositories/CanDoItAll/src/Modules/CanDoItAll.Modules.AgentFramework.ProviderManagement/SharedProviders/SharedProviderInvocationAuditService.cs).
[Relay persistence fixture](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/SharedProviderPersistenceIntegrationTests.cs).
[Migration fixture (actual class MigrationBootstrapIntegrationTests)](C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/DatabaseMigrationIntegrationTests.cs).
[Profile scope fixture](C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/LlmChatWholeUseCaseProfileScopeTests.cs).
Normative [boundary map](../../architecture/01-csharp-boundary-map.md),
  [lifecycle](../../architecture/05-history-data-lifecycle.md),
  [query/security](../../architecture/09-search-security-contract.md) and
  [pricing/capture](../../architecture/10-pricing-and-capture-contract.md).

## Deliverables

- Create additive entries/owner links/detail parts/outbox/checkpoints/policy mappings and required unique/paging/expiry indexes.
- Implement same-context metadata outbox staging, optimistic transitions, cross-instance leases and bounded read/write/policy ports.
- Store optional protected input once per operation/input revision and per-attempt response; apply byte bounds, short expiry and atomic partition quota.
- Implement retention eligibility and read-time expiry, pending-owner recovery and replay/tombstone safety; keep production destructive cleanup disabled until SB05 passes.
- Register the EF configuration assembly through composition and migration model; provide governed activation/rollback/restore instructions without dropping original audit rows.

## C# Architecture Impact

History.Persistence owns EF and protection; Application owns policy/state decisions. Existing Infrastructure stays neutral. Do not add a general repository layer or another transcript schema.

## Boundary Ownership

Entries distinguish MetadataAuthority from RetentionAuthority. Canonical owners keep bodies; relay keeps its canonical audit. Stable storage lineage, not runtime generation, keys the persisted partition.

## Dependency Direction

Persistence references approved Abstractions/Application/Infrastructure only. Same-context staging accepts AppDbContext only at the persistence integration surface; neutral producer ports stay EF-free.

## Pattern Decision

ADR01/03/05/06: compact read model, durable intent, bounded detail and explicit persistence boundary. A separate DbContext is not an atomic outbox.

## Testability Contract

New ProviderHistoryPersistenceIntegrationTests owns the new schema. Proposed cases: Source_and_intent_commit_atomically; Quota_is_atomic_across_captures; Expired_detail_is_unreadable_before_cleanup; Profile_switch_does_not_redirect_finalization; Migration_preserves_existing_audit_and_legacy_unknowns.

## Partial Class Policy

No new runtime partial. Existing Razor code-behind/generated files are exceptions only for
their established framework role. New cohesive classes follow the 250-line review and
400-line redesign/exception gate; extraction removes the original behavior.

## Architecture Proof Required

- Record actual changed files, public signatures and project edges against the allowed
  dependency table. Review DI factories and old call sites, not only the new collaborator.
- Verify actual production and migration model inclusion, scalar query shape, no provider execution from workers and no reverse Infrastructure reference.

## Dependency Impact

- SB04 depends on durable start/finalization ports; SB05 depends on source intents and tombstones; SB06 depends on bounded indexed reads.
- Retention activation is blocked until SB05 owner deletion and late-commit semantics pass. SB07 policy editor uses the same versioned policy store.

## Validation Depth

- Proof tier: `Governed`.
- Critical foundation: Yes; schema, durable writes, profile isolation, quotas and retention authority..
- Test project/filter: `C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj` / `FullyQualifiedName~ProviderHistoryIdentityTests|FullyQualifiedName~SharedProviderArchitectureCharacterizationTests|FullyQualifiedName~ProviderHistoryPolicyTests|FullyQualifiedName~ProviderHistoryLifecycleTests|FullyQualifiedName~LlmChatWholeUseCaseProfileScopeTests|FullyQualifiedName~ProviderDatabaseTransferTests`; `C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj` / `FullyQualifiedName~ProviderHistorySourceProjectionIntegrationTests|FullyQualifiedName~ProviderHistoryPersistenceIntegrationTests|FullyQualifiedName~SharedProviderPersistenceIntegrationTests|FullyQualifiedName~MigrationBootstrapIntegrationTests`.
- Selection reason: New history persistence/lifecycle behavior plus existing canonical audit, migration, profile-scope and actual provider database-transfer compatibility. Existing ProviderDatabaseTransferTests remain a compatibility gate; the history transfer cases run in the new PostgreSQL persistence fixture to exercise actual protected storage, partition identity, policy and replay state.
- Expected discovery: Existing SharedProviderPersistenceIntegrationTests and MigrationBootstrapIntegrationTests are discovered; Provider_management_marker_discovers_the_compatible_provider_schema and Provider_transfer_copies_profiles_and_referenced_secrets_but_not_workspace_preference are present, plus all five proposed cases above, History_transfer_preserves_partition_identity_policy_and_replay_state and policy invalid-value/concurrency cases. Record exact actual cases/counts at execution;
  zero discovery or a missing required behavior fails the gate. Executed discovery and raw TRX agree on method identities and case multiplicities; managed output redacts some theory arguments. The manifest maps the final case names to these behaviors.
- Invalidation keys: HistorySchemaV1; SameContextOutbox; RetentionAuthority; DetailQuotaProtection; ProfileLeaseFence; MigrationModel.
- Broad-gate decision: Required once at frozen SB08 only if public-contract/schema/DI
  changes made here trigger it. No broad suite here or repeated run without invalidation.
- Future focused commands (after implementing the named cases; use the same unchanged
  source revision for discovery/build and the subsequent no-build execution):

```powershell
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --list-tests --filter 'FullyQualifiedName~ProviderHistoryIdentityTests|FullyQualifiedName~SharedProviderArchitectureCharacterizationTests|FullyQualifiedName~ProviderHistoryPolicyTests|FullyQualifiedName~ProviderHistoryLifecycleTests|FullyQualifiedName~LlmChatWholeUseCaseProfileScopeTests|FullyQualifiedName~ProviderDatabaseTransferTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Unit/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj' --no-build --filter 'FullyQualifiedName~ProviderHistoryIdentityTests|FullyQualifiedName~SharedProviderArchitectureCharacterizationTests|FullyQualifiedName~ProviderHistoryPolicyTests|FullyQualifiedName~ProviderHistoryLifecycleTests|FullyQualifiedName~LlmChatWholeUseCaseProfileScopeTests|FullyQualifiedName~ProviderDatabaseTransferTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --list-tests --filter 'FullyQualifiedName~ProviderHistorySourceProjectionIntegrationTests|FullyQualifiedName~ProviderHistoryPersistenceIntegrationTests|FullyQualifiedName~SharedProviderPersistenceIntegrationTests|FullyQualifiedName~MigrationBootstrapIntegrationTests'
dotnet test 'C:/repositories/CanDoItAll/tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj' --no-build --filter 'FullyQualifiedName~ProviderHistorySourceProjectionIntegrationTests|FullyQualifiedName~ProviderHistoryPersistenceIntegrationTests|FullyQualifiedName~SharedProviderPersistenceIntegrationTests|FullyQualifiedName~MigrationBootstrapIntegrationTests'
```

## Implementation Steps

1. Add mappings/unique keys and an additive migration with explicit legacy defaults; inspect generated SQL.
2. Implement durable start/terminal/owner mutation and exact same-context staging; inject TimeProvider and bounded retry policy.
3. Implement protected bounded detail, shared retry input, transactional quota and expiration predicates.
4. Add leased recovery/maintenance primitives with stable partition and transient fence; do not activate destructive cleanup yet.
5. Test transaction rollback, source/version conflict, expiry/quota races, migration/restore and production registration on disposable stores.

## Acceptance Checklist

- [x] Entry/source uniqueness excludes mutable source version; sort timestamps are immutable with explicit TimeBasis.
- [x] Existing canonical histories and relay rows remain intact; no body enters metadata tables/outbox.
- [x] Start writes fail explicitly before provider use; terminal errors remain recoverable without provider replay.
- [x] Canonical retention is independent of the direct/relay default; shared input expiry cannot be extended by retries.
- [x] Protection-key failure never stores/returns plaintext; expiry applies before physical GC.
- [x] Migration and rollback activation are additive, registered and tested in a disposable environment.

## Proof Required

- Store a proof manifest, exact command transcripts, discovered cases/exit codes, changed-source revision, artifact paths/hashes and semantic positive/negative evidence under `proof/SB03/` at the bundle root.
- Include actual migration SQL, model-registration test, transaction/fault/quota/profile transcripts, FK/index plan and positive/negative data-lifecycle fixtures with hashes. Redact all sensitive fixture values in human-readable reports.
- Follow [validation strategy](../../plan/02-validation-strategy.md); distinguish existing
  test anchors from proposed new cases, and source proof from executed behavior.

## Browser Validation Logging

N/A for direct UI changes in this phase. Production host/SQL/lifecycle proof is required where listed; the two-tab desktop acceptance remains SB07/SB08.

## Scope Exceptions

- This phase alone does not close the complete product request. Deferred IDM/EGCP person
  mapping, global federation, exact wire replay, mobile redesign and unrelated refactors
  remain outside the bundle.
- No paid inference, user-database mutation or deployment without explicit authorization.

## Do Not Do

- Do not migrate all relay rows into a competing standalone store.
- Do not delete active attempts, extend expiry on replay, silently evict oldest details, or count metadata projections as new usage.
- Do not run migration/cleanup against the user's active database during this phase without explicit authorization.

## Progression Gate

- SB04/SB05 may start only after durable store, same-context outbox, profile fencing, protected detail and additive migration gates pass; cleanup remains inactive until source lifecycle proof.
- Update [execution report](../../reviews/01-execution-report.md) with actual proof and
  downstream dependencies checked. A planned command or passed intermediary is not closure.

## Reopen Triggers

- Schema/key/retention authority, protection/quota, profile transfer or transaction boundary changes invalidate persistence and all dependent capture/query/UI proof.
