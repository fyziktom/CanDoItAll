# Target Solution

## End State

`ProcessRunAutomationDispatchService.ArtifactProjection.cs` still orchestrates projection sources, but all storage-backed artifact writes flow through `ProcessArtifactProjectionWriteCoordinator`. Completed decision artifacts use a separate record-only helper. Source-specific planning remains in source adapters and projection planner.

## Desired Layering

```text
Dispatcher orchestration
  -> source discovery / duplicate checks / file path resolution / candidate state update
  -> projection source adapters and planner
  -> write coordinator for storage-backed writes
  -> record-only helper for non-storage artifact records
```

## Non-goals

This is not a Process Core extraction. It is a stronger boundary inside the current Processes module.
