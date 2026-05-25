# Proof manifest SB05

## Status

Complete.

## Commands

- `Get-ChildItem .\src\CanDoItAll.Migrations.PostgreSql\Migrations -File`
- `dotnet test .\tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-build --filter FullyQualifiedName~MigrationBootstrapIntegrationTests -v:minimal`
- `dotnet ef migrations list --project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --context CanDoItAll.Infrastructure.Persistence.AppDbContext --no-connect`
- `dotnet ef migrations add __PostgreSqlModelDriftCheck --project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --context CanDoItAll.Infrastructure.Persistence.AppDbContext`
- `dotnet ef migrations script --idempotent --project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --context CanDoItAll.Infrastructure.Persistence.AppDbContext --output .\codex\bundles\db-remove-sqlite-followup-bundle-v1\evidence\SB05\postgresql-baseline-idempotent.sql`

## Evidence files

- `evidence/SB05/postgresql-migration-files.txt`
- `evidence/SB05/dotnet-ef-migrations-list.log`
- `evidence/SB05/postgresql-baseline-idempotent.sql`
- `evidence/SB08/dotnet-test-integration-nonquarantined.log`

## Notes

Only the baseline migration and model snapshot remain in `src/CanDoItAll.Migrations.PostgreSql/Migrations`. The temporary `__PostgreSqlModelDriftCheck` migration had empty `Up`/`Down` methods and was removed after verification. `dotnet ef migrations list --no-connect` reports `20260523211921_InitialPostgreSqlBaseline`; the EF tool emits a non-blocking 10.0.3 versus 10.0.4 version warning.
