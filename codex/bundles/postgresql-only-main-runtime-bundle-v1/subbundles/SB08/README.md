# SB08 - Consolidate PostgreSQL Migrations Into One Baseline

## Objective

After SQLite removal and model stabilization, consolidate PostgreSQL migrations into one clean baseline.

## Preconditions

- SB01-SB07 completed.
- Build passes.
- No SQLite runtime/profile/snapshot branches remain.
- PostgreSQL model is stable.
- Tests relevant to persistence pass.

## Required steps

1. Capture existing PostgreSQL migration inventory.
2. Delete old PostgreSQL migrations only after confirming model stability.
3. Generate one clean PostgreSQL baseline migration.
4. Validate fresh PostgreSQL DB creation from zero.
5. Validate app startup against fresh DB.
6. Validate representative persistence/process/workflow paths.
7. Write manual real DB alignment guide.

## Manual real DB alignment

Create:

```text
evidence/manual-real-db-alignment.md
```

Include:

- What changed.
- Recommended backup step.
- Whether to dump/recreate/import.
- How to align `__EFMigrationsHistory`.
- What manual SQL might be required.
- What must be verified before production-like use.

## Validation

```powershell
dotnet ef migrations list --project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj
dotnet ef database update --project .\src\CanDoItAll.Migrations.PostgreSql\CanDoItAll.Migrations.PostgreSql.csproj --startup-project .\src\CanDoItAll.Web\CanDoItAll.Web.csproj
dotnet build .\CanDoItAll.slnx
dotnet test .\CanDoItAll.slnx --filter "Category!=Browser&Category!=LiveProcess"
```

Adjust commands if repository uses a different migration command pattern.

## Required proof

```text
proof/SB08/manifest.md
proof/SB08/semantic-invariants.md
evidence/SB08/postgresql-migration-inventory-before.md
evidence/SB08/postgresql-baseline-proof.log
evidence/manual-real-db-alignment.md
```
