# SB03 - Remove or isolate database snapshot runtime stubs

## Goal

Remove current database snapshot service/model runtime surface because snapshots are deferred and can be reimplemented later.

## Context

The current branch reduced snapshots to deferred failures, but the service, request/result models, and transport enum remain in Infrastructure.

## Required changes

1. Audit references to:
   - `IDatabaseSnapshotService`
   - `DatabaseSnapshotService`
   - `DatabaseSnapshotTransportKind`
   - `DatabaseSnapshotManifest`
   - clone/materialization request/result models.
2. If no live feature requires them, remove these types and DI registration.
3. If compile dependencies require them temporarily, move them behind a clearly named future-work/deprecated namespace and remove any runtime/UI path.
4. Remove snapshot source kinds from profile model in SB01.
5. Add future-work documentation for portable export/import snapshots outside runtime DB provider model.

## Validation

- Build succeeds without snapshot runtime service if removed.
- `rg -n "DatabaseSnapshot|SnapshotCache|IpfsSnapshot"` returns only allowed docs/future-work files.
- Data Sources UI contains no snapshot section/actions.

## Proof artifacts

Write:

- `proof/SB03/manifest.md`
- `proof/SB03/semantic-invariants.md`
- relevant logs under `evidence/SB03/`

## Acceptance criteria

- Snapshot support is not part of runtime database profile model.
- No SQLite-backed or provider-backed snapshot materialization path remains.

## Status

- Completed

## Objective

Remove deferred database snapshot service/model/runtime surface from the active application.

## Covered Inputs

- `bundle://requirements/01-followup-requirements.md`

## Prerequisites

- SB01 provider source cleanup completed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Workspace/Database/DatabaseProfileWorkspaceService.cs`
- `repo://src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`

## Deliverables

- Snapshot service/model deletion.
- DI cleanup.
- Workspace and UI references removed.

## Dependency Impact

- Data Sources and workspace profile services no longer expose snapshot operations.

## Validation Depth

- Build and residue audit.

## Implementation Steps

- Audit snapshot references.
- Remove unused runtime service/models and DI.
- Verify no runtime/UI snapshot controls remain.

## Do Not Do

- Do not retain dormant snapshot runtime types as active profile model concepts.

## Acceptance Checklist

- Snapshot service is not registered.
- Snapshot source kinds are not in the database profile model.
- Data Sources renders no snapshot action controls.

## Proof Required

- `bundle://proof/SB03/manifest.md`

## Browser Validation Logging

- Snapshot absence is also covered by SB02/SB08 browser evidence.

## Progression Gate

- Build must pass after snapshot runtime removal.

## Suggested Agent Prompt

Implement SB03, then run the build and residue proof commands in `proof/SB03/manifest.md`.
