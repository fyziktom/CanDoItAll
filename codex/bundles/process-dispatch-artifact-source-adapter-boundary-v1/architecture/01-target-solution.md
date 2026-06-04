# Target Solution

## Shape After This Bundle

```text
ProcessRunAutomationDispatchService.ArtifactProjection
  -> orchestrates dispatch claim, file reads, storage writes, DB recording
  -> calls source adapters to prepare projection plans
  -> calls write coordinator for the first migrated write path

ProcessArtifactProjectionPlanner
  -> pure source-independent plan construction

ProcessArtifactProjectionSourceAdapters
  -> process mock
  -> workspace-written
  -> existing-managed
  -> assistant-response
  -> provider-native browser

ProcessArtifactProjectionWriteCoordinator
  -> storage placement + process artifact record delegation
  -> first used by execution-artifact projection only
```

## Boundary Rules

- Source adapters must not write files, call storage, mutate DB, transition steps, or inspect DI.
- The write coordinator may perform storage and artifact-record side effects, but must not decide projection source semantics.
- Dispatcher keeps orchestration and dispatch claim renewal.
- No helper should depend on MAF runtime or UI layers.
