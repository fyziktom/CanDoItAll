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

The migration chain starts with one squashed baseline,
`20260728161028_InitialPostgreSqlBaseline`. It describes the complete database required by
the current application model. Provider-specific indexes that EF cannot represent in the
model are owned by
[PostgreSqlMigrationBaseline.cs](../CanDoItAll.Infrastructure/Persistence/PostgreSqlMigrationBaseline.cs)
and are applied by the baseline migration.

### Existing development database

Back up the database before the first startup after the squash. Startup recognizes only
the complete 42-migration development chain that ended at
`20260727232724_AddProviderProfileConcurrencyToken`. It validates the baseline schema,
atomically replaces those history rows in `__EFMigrationsHistory` with the single
squashed-baseline row, and then lets EF apply any migrations created after the baseline.

Partial or unexpected migration histories are rejected with an actionable error. The
bootstrapper does not silently mark an incomplete schema as current. No manual history
SQL is required for the supported complete legacy chain.

New migrations must be added after this baseline through the normal EF workflow. Do not
edit the baseline after another database has started consuming later migrations.

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
- Current architecture: `docs/architecture-beta.md`
