# Validation Commands

Run from repository root after placing this bundle at:

`codex/bundles/processes-hardening-followup-scope-resilience`

## Prepared Bundle Validation

```powershell
python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py --stage prepared codex/bundles/processes-hardening-followup-scope-resilience
```

## Targeted Integration Tests

```powershell
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"
```

## Full Build

```powershell
dotnet build CanDoItAll.slnx --no-restore
```

## PostgreSQL-only Audit

```powershell
rg -n "Sqlite|SQLite|Migrations\.Sqlite|UseSqlite" src tests codex -S
```

Any SQLite result must be justified as removed documentation, historical text, or unrelated legacy reference. No new SQLite runtime/migration code may be introduced.
