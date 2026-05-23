# SB07 - Remove or Explicitly Defer SQLite-Backed Database Snapshot Flows

## Objective

Remove current snapshot flows that keep SQLite alive as a runtime/profile provider.

## Inputs

Known files:

```text
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileModels.cs
src/CanDoItAll.Infrastructure/ControlPlane/DatabaseProfileControlPlaneService.cs
src/CanDoItAll.Modules.Workspace/Pages/Components/DatabaseSourcesSettingsPanel.razor
src/CanDoItAll.Modules.Workspace/DatabaseProfileWorkspaceService.cs
```

## Required removals/deferments

Remove or explicitly disable/defer:

```text
SnapshotCache
IpfsSnapshot
SQLite materialized snapshot profiles
SQLite clone database profiles
restore paths using SQLite-specific PRAGMA behavior
```

## Required future note

Add a clear future-work note in docs or architecture:

```text
Future snapshots should be implemented as a separate bounded context or portable export/import package, not as a main AppDbContext runtime provider.
```

## Do not

- Do not implement a replacement snapshot system in this subbundle.
- Do not preserve snapshot flow by keeping SQLite hidden.

## Validation

```powershell
rg -n -i "SnapshotCache|IpfsSnapshot|materialized snapshot|sqlite.*snapshot|PRAGMA foreign_keys|CloneDatabase|DatabaseSnapshot" src tests docs
dotnet build .\CanDoItAll.slnx
```

## Required proof

```text
proof/SB07/manifest.md
proof/SB07/semantic-invariants.md
evidence/SB07/snapshot-defer-audit.log
```
