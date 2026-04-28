# 02-project-zip-package-import-export

## Status

- `Completed`

## Objective

Implement project-scoped zip export/import for all projects using the same table inventory and ordering rules proven by subbundle `01`.

## Covered Inputs

- `N001`: all projects import/export
- `N002`: zip import/export

## Prerequisites

- `01-project-database-transfer` is completed with critical foundation proof.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\DatabaseSnapshots.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\ControlPlane\ControlPlanePaths.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Transfers\StorageTransferPipeline.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Services\WorkbenchModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectNodes\ProjectNodeBindings.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\DatabaseRuntimeSwitchingIntegrationTests.cs`

## Deliverables

- A project package service that creates `.zip` packages with manifest and project table payloads.
- Import support that restores all project package payloads into a target profile/current profile.
- Storage/media copy support for project media files when referenced by node bindings and available locally.
- Integration coverage for export followed by import into an empty profile.

## Dependency Impact

- Subbundle `03` depends on a stable service API to wire UI actions.
- Subbundle `04` depends on package proof to close `N002`.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Define package manifest/result models.
2. Implement all-project export to a control-plane package path, using safe zip creation.
3. Add table payload serialization for the project table inventory.
4. Add storage/media file inclusion for referenced project media under `managed-files` when present.
5. Implement safe extraction/import with zip-slip protection.
6. Restore target records in the same dependency-safe order as the database transfer foundation.
7. Add integration tests that export, inspect package existence/manifest, import into a target profile, and verify project/workbench service reads.

## Scope Exceptions

- No IPFS package transport is required for project packages in this phase.
- Browser download/upload is not required; path-based package export/import is acceptable if exposed through UI in subbundle `03`.

## Do Not Do

- Do not reuse whole database snapshots as the only project export/import implementation.
- Do not package unrelated database tables.
- Do not weaken `all projects` into current filtered project list.

## Acceptance Checklist

- Export creates a `.zip` file with manifest and table payloads.
- Import rejects missing/unsafe packages.
- Import into empty target recreates all project/workbench records in scope.
- Package tests prove both data and manifest counts.

## Proof Required

- `dotnet build src/CanDoItAll.Modules.Workbench/CanDoItAll.Modules.Workbench.csproj --no-restore`
- `dotnet build tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-restore --no-dependencies -v:minimal`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --no-build --filter "FullyQualifiedName~DatabaseTransferIntegrationTests.Project_package_export_import_round_trips_project_records_and_media" --logger "console;verbosity=normal"`

## Browser Validation Logging

- `N/A` for this subbundle. UI proof is owned by `03-ui-exposure-and-workflow-proof`.

## Progression Gate

- Passed. Project package export/import passes a round-trip integration test with manifest, table payload, and media-file assertions.

## Suggested Agent Prompt

```text
Implement subbundle 02 only: add all-project zip export/import service and prove package round trip. Reuse the table inventory and copy ordering from subbundle 01. Do not wire UI yet.
```
