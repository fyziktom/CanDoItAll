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
