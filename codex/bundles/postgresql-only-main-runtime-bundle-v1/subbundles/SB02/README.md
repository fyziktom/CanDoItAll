# SB02 - PostgreSQL-Only Database Profile and Control-Plane Contract

## Objective

Remove SQLite from the profile/control-plane contract.

## Inputs

Known files:

```text
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseDrivers.cs
src/CanDoItAll.Modules.Workspace/DatabaseProfileWorkspaceService.cs
```

## Required changes

Remove or reject these concepts from main runtime:

```text
DatabaseProviderKind.Sqlite
ManagedSqlite
ExternalSqliteFile
ImportedSqlite
SnapshotCache
IpfsSnapshot
SqliteDatabaseProfileConnection
SqliteDatabasePath
CreateManagedSqliteProfileLocked
TryCreateLegacyProfileLocked
TryResolveCatalogBackedSqliteOverrideLocked
BuildSqliteOverrideProfile
```

Expected behavior:

- Runtime profile resolution never returns SQLite.
- PostgreSQL profiles still work.
- Unsupported legacy SQLite catalog entries fail with clear guidance.
- Catalog loading should not silently auto-create SQLite profiles.
- Default current profile should be PostgreSQL.

## Edge cases

If local profile JSON still contains SQLite:

- Do not silently ignore the problem if it would cause confusing runtime behavior.
- Emit clear unsupported legacy message.
- Optionally offer a documented cleanup command or instructions.

## Validation

```powershell
rg -n -i "DatabaseProviderKind\.Sqlite|ManagedSqlite|ExternalSqliteFile|ImportedSqlite|SnapshotCache|IpfsSnapshot|SqliteDatabaseProfileConnection|SqliteDatabasePath" src tests
dotnet build .\CanDoItAll.slnx
```

## Required proof

```text
proof/SB02/manifest.md
proof/SB02/semantic-invariants.md
evidence/SB02/profile-contract-audit.log
```
