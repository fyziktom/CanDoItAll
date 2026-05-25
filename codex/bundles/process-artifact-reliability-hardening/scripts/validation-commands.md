# Validation Commands

Run these from the repository root after executing the relevant subbundles.

## Prepared Bundle Validation

```powershell
python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py --stage prepared codex/bundles/process-artifact-reliability-hardening
```

## Focused Process Tests

```powershell
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"
```

## Full Build

```powershell
dotnet build CanDoItAll.slnx --no-restore
```

## PostgreSQL Validation

If EF/data model changes are made, run the repository's current PostgreSQL migration/model validation command. Do not add SQLite validation.

## SQLite Residue Audit

```powershell
rg -n "Sqlite|SQLite|Migrations.Sqlite|UseSqlite" src tests codex/bundles/process-artifact-reliability-hardening -S
```

Expected result for changed scope: no newly introduced SQLite work. Existing repository historical references, if any, must be classified and not expanded.
