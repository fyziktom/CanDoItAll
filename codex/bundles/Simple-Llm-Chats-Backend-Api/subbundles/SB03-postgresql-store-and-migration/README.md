# SB03 — PostgreSQL store and migration

Proof tier: **Governed**

## Objective

Implement cross-process-safe PostgreSQL persistence in a separate project and append a normal EF migration.

## Scope

- Add CanDoItAll.Modules.LlmChats.Persistence as a non-Razor SDK project.
- Implement EF entities/configurations for the locked table set.
- Persist the nullable revision thinking-effort override and requested/effective invocation effort
  without collapsing provider default into explicit `None`.
- Implement product repositories and unit of work.
- Implement EfLlmConversationStore over transcript/message tables with conditional revision CAS.
- Register the persistence assembly with AppDbContextModelRegistry in runtime composition and the PostgreSQL design-time factory.
- Append migration after the baseline and update the model snapshot.
- Implement a versioned database-transfer handler using the existing transfer contract.
- Do not edit the baseline migration.

## Expected change surface

- new persistence project and README
- EF entities/configurations/repositories/store/unit-of-work
- composition model-assembly registration
- Migrations.PostgreSql project reference, design-time factory, new migration, and snapshot
- focused PostgreSQL and database-transfer integration tests

## Targeted validation

- EfLlmConversationStoreIntegrationTests
- LlmChatPersistenceIntegrationTests
- LlmChatsDatabaseTransferIntegrationTests
- focused MigrationBootstrapIntegrationTests cases
- dotnet ef migrations has-pending-model-changes

All test commands must comply with `test-budget.json`. Record exact commands and results in the proof
manifest.

## Acceptance criteria

- [x] CAS works across two independent DbContext/store instances.
- [x] Concurrent sends produce one winner and a typed conflict.
- [x] Compensation removes only the exact pending entry.
- [x] Definition revisions are append-only.
- [x] Migration works on empty and baseline databases.
- [x] Versioned database-transfer round-trip preserves IDs, revisions, operations, audit, and referential integrity.
- [x] No file path or OS-specific persistence is used.

## Forbidden work

- in-memory lock as correctness boundary
- SQLite as migration proof
- editing the baseline migration
- API or UI

## Handoff

Complete `SESSION-HANDOFF.md` and `proof/proof-manifest.json`. Do not unlock the next subbundle until
all acceptance criteria and any checkpoint are satisfied.
