# CanDoItAll.Migrations.PostgreSql

## Purpose

PostgreSQL EF Core migrations for the CanDoItAll application model.

## Project Type

- SDK: `Microsoft.NET.Sdk`
- Target framework(s): `net10.0`
- Validation command:

```powershell
dotnet build src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj
```

## Dependencies

The authoritative project and package dependency list is in [CanDoItAll.Migrations.PostgreSql.csproj](CanDoItAll.Migrations.PostgreSql.csproj). This README focuses on the project's purpose, boundaries, and validation.

## Architecture Notes

This project owns provider-specific EF Core migration assets only. Runtime behavior belongs in Infrastructure or the owning product module.

`20260728161028_InitialPostgreSqlBaseline` defines the complete baseline database required
by the application model. Provider-specific indexes that EF cannot represent in the
model are owned by
[PostgreSqlMigrationBaseline.cs](../CanDoItAll.Infrastructure/Persistence/PostgreSqlMigrationBaseline.cs)
and are applied by the baseline migration.

Startup validates the baseline schema and migration identity before applying pending
migrations. Partial or unexpected migration state fails with an actionable error; the
bootstrapper never marks an incomplete schema as current.

Create new migrations through the normal EF workflow and append them after the baseline.
Do not edit an applied migration. Back up authoritative data before applying schema
changes.

The LLM Chats schema is an append-only migration chain:

- `20260814163458_AddLlmChats` creates definitions, revisions, tags, conversations, transcripts,
  messages, operations, and invocation audit.
- `20260815002135_CanonicalizeLlmChatConversationMetadata` aligns transcript metadata with canonical
  conversation state.
- `20260815005922_AddLlmChatCancellationGeneration` adds durable cancellation fencing.
- `20260815023403_AddLlmChatExecutionLeases` adds recoverable multi-host dispatch ownership.
- `20260815051653_AddLlmChatOperationEvents` adds the durable replay journal and retention indexes.
- `20260815233557_CompleteLlmChatOperationEvidence` completes durable delivery, finish-reason, and
  event high-water evidence.
- `20260817183339_AddSimpleChatInvocationPricingEvidence` adds usage and immutable pricing evidence
  for Simple Chat provider invocations.
- `20260818103613_AddSimpleChatProjectAttribution` records immutable workspace attribution for
  Simple Chat operations and indexes project-scoped activity reporting.

The model snapshot and database-transfer contract must represent the same final shape. Never remove or
rename an applied migration to make a pending-model check pass.

## Migration Validation

The focused integration tests require the repository's PostgreSQL test service:

```powershell
dotnet test tests/Integration/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter FullyQualifiedName~MigrationBootstrapIntegrationTests
```

Verify that the model and snapshot still match:

```powershell
dotnet ef migrations has-pending-model-changes --project src/Foundation/CanDoItAll.Migrations.PostgreSql/CanDoItAll.Migrations.PostgreSql.csproj --startup-project src/App/CanDoItAll.Web/CanDoItAll.Web.csproj --context AppDbContext
```

## Related Docs

- Repository overview: `README.md` at the repo root
- Current architecture: `docs/architecture/overview.md`
