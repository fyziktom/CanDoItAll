# SB05 — Simple Chats persistence and pricing evidence

## Status

- Prepared
- Stage: persistence-checkpoint
- Proof tier: Governed

## Objective

Move the EF/data-profile boundary into MAF Persistence, preserve all LlmChats_* data contracts, and make each new invocation a trustworthy immutable usage/pricing audit item.

## Owned Requirements

- ASCC-002
- ASCC-008
- ASCC-011
- ASCC-014
- ASCC-016
- ASCC-022
- ASCC-024
- ASCC-025
- ASCC-026
- ASCC-027
- ASCC-029
- ASCC-030
- ASCC-045
- ASCC-046

## Prerequisites

- SB04

## Current Source Anchors

- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Entities/LlmChatPersistenceRows.cs
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/EntityConfigurations/
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Repositories/
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/ReadModels/
- repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/DatabaseTransfer/
- repo://src/Foundation/CanDoItAll.Migrations.PostgreSql/Migrations/
- repo://src/Foundation/CanDoItAll.Infrastructure/Persistence/AppDbContext.cs

## Explicit Non-Goals

- Do not rename existing tables or historical migrations.
- Do not reprice historical invocations.
- Do not make transcript messages a usage ledger.
- Do not add a central usage table/outbox.
- Do not change HTTP/UI behavior.

## Implementation Steps

1. Create SimpleChats.Persistence and move EF/data-profile implementations with stable mappings.
2. Prove namespace/project relocation alone produces no destructive schema change.
3. Extend invocation evidence append-only with available reasoning/cache-write/total tokens, usage status, cost, pricing status/version/hash, and provider/model snapshot data.
4. Persist new pricing evidence atomically with each invocation record; rollback leaves neither partial audit nor phantom cost.
5. Define legacy conversion: any stored tokens -> legacy usage-known/pricing-unpriced; all-zero without corroborating evidence -> usage-unknown.
6. Preserve OperationId + Ordinal primary identity and count attempts, not transcript/terminal duplicates.
7. Update EF configuration/model snapshot and add one append-only PostgreSQL migration.
8. Update transfer export/import and database profile generation/fresh-scope/lease/heartbeat/commit-fence implementations.
9. Add deterministic idempotent upgrade, transfer, rollback, concurrency, and PostgreSQL tests.
10. Update AppDbContext/migrations references without activating unified queries/UI.

## Acceptance Criteria

- [ ] No existing LlmChats_* table rename/drop.
- [ ] New invocation evidence is atomic and immutable.
- [ ] Legacy cost remains unpriced and visible.
- [ ] Retry/failed/cancelled reported usage is retained per attempt exactly once.
- [ ] Transfer and migrations are backward compatible/idempotent.
- [ ] CP1 architecture/data gates Pass.

## Validation Depth

- Proof tier: Governed.
- Critical foundation: yes; unified cost truth and every later data/UI proof depend on it.

Governed data-boundary proof with SQL/migration/model transcripts, PostgreSQL tests, rollback/concurrency/transfer invariants, project graph, hashes, and architecture gate.

## Focused Test Selection

Workspaces:

- tests/Solutions/CanDoItAll.Tests.Unit.slnx
- tests/Solutions/CanDoItAll.Tests.Integration.slnx

Required:

- LlmChatPersistenceIntegrationTests
- LlmChatTransactionalConcurrencyIntegrationTests
- DatabaseMigrationIntegrationTests
- LlmChatsApiPostgreSqlIntegrationTests
- LlmChatWholeUseCaseProfileScopeTests
- LlmChatBackendCompositionTests

Add exact cases InvocationPricingEvidenceCommitsAtomically, LegacyTokensRemainUnpriced, AllZeroLegacyAttemptIsUsageUnknown, TransferRoundTripPreservesPricingStatus, RelocationDoesNotRenameTables.

Expected discovery: non-zero for every selector and all five new cases.

## Invalidation And Broad-Gate Decision

Stable/Playwright forbidden. Reopen on any row/config/migration/snapshot/transfer/profile/lease/usage/pricing field or transaction change.

## UI Composition Contract

No UI change. New unknown/unpriced states become available to later dashboard work but are not rendered here.

## C# Architecture Impact

Moves outer EF boundary and adds durable evidence without taking provider runtime ownership.

## Boundary Ownership

Persistence owns database implementations and source data. Runtime owns provider execution. Usage owns neutral projection contracts.

## Dependency Direction

Persistence -> Application/Core/Usage/Infrastructure; never -> Runtime/Components/Agent module/Web.

## Pattern Decision

Repository/port adapters with immutable audit evidence. Expand-migrate-contract for schema/project move.

## Testability Contract

Core/Application tests remain DB-free; Persistence gets relational tests. Negative tests detect transcript double count, query-time repricing, and non-atomic writes.

## Partial Class Policy

No partial files. Split transfer/persistence collaborators by cohesive behavior only and retain direct tests.

## Architecture Proof Required

Before/after graph and schema, direct owner tests, old-owner shrink, no destructive SQL, no-new-partial/cycle, transfer invariants, architecture gate.

## Progression Gate

- CP1 Pass unlocks SB06; SB07 may also proceed because Core/Application/Persistence contracts are now stable.

## Reopen Triggers

- schema/model pending change;
- price/status semantics change;
- duplicate attempt counting;
- transfer/profile/lease regression;
- old runtime construction reappears;
- forbidden reference.

## Covered Inputs

- Raw request: move persistence under MAF, correct its mixed ownership, and include trustworthy Simple Chat costs.
- Requirements ASCC-002, ASCC-008, ASCC-011, ASCC-014, ASCC-016, ASCC-022, ASCC-024–030, ASCC-045–046.

## Exact Source References

- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats.Persistence\Entities\LlmChatPersistenceRows.cs
- C:\repositories\CanDoItAll\src\Modules\CanDoItAll.Modules.LlmChats.Persistence\EntityConfigurations
- C:\repositories\CanDoItAll\src\Foundation\CanDoItAll.Migrations.PostgreSql\Migrations
- C:\repositories\CanDoItAll\src\Foundation\CanDoItAll.Infrastructure\Persistence\AppDbContext.cs

## Deliverables

- MAF SimpleChats.Persistence, append-only immutable usage/pricing evidence, deterministic legacy migration, stable transfer/profile/lease behavior.

## Dependency Impact

- SB06 analytics and SB07-SB11 product proof are invalid if migration, pricing status, or attempt identity is weak.

## Acceptance Checklist

- All Acceptance Criteria above pass, including no table rename/drop, atomic evidence, unpriced legacy, idempotent migration/transfer.

## Proof Required

- proof/SB05/manifest.md, SQL/model/migration/transfer transcripts, PostgreSQL/rollback/concurrency tests, hashes/invariants, architecture gate.
