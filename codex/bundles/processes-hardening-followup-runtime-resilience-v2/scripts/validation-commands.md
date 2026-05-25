# Validation Commands

Run from repository root.

## Prepared Bundle

```powershell
python codex/skills/bundles/candoitall-bundle-preparation/scripts/validate_bundle.py --stage prepared codex/bundles/processes-hardening-followup-runtime-resilience-v2
```

## Focused Runtime Tests

```powershell
dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests|FullyQualifiedName~ProcessDefinitionLinterTests"
```

## Unit Tests

```powershell
dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --no-restore
```

## Build

```powershell
dotnet build CanDoItAll.slnx --no-restore
```

## PostgreSQL Only Audit

```powershell
rg -n "Sqlite|SQLite|UseSqlite|Migrations.Sqlite" src tests codex/bundles/processes-hardening-followup-runtime-resilience-v2 -S
```

Any SQLite hit introduced by this bundle is a blocking failure unless it is only a historical note in deleted/old bundle text and explicitly justified.
