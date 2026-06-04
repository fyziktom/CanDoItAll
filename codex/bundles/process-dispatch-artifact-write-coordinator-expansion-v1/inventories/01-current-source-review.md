# Current Source Review

Current source review result:

- Previous source-adapter bundle completed.
- Source adapters exist for process mock, workspace-written, existing-managed, response-text, and provider-native browser projection sources.
- Write coordinator exists but is only used by the execution-artifact projection path.
- Dispatcher still directly performs storage placement and artifact record construction in multiple projection paths.

Important source files:

| File | Current role | Next use |
| --- | --- | --- |
| `ProcessArtifactProjectionWriteCoordinator.cs` | First storage-backed write seam | Harden and reuse across paths |
| `ProcessArtifactProjectionSourceAdapters.cs` | Source-specific projection plans | Keep source semantics here |
| `ProcessRunAutomationDispatchService.ArtifactProjection.cs` | Orchestration plus remaining side effects | Migrate side effects gradually |
| `ProcessArtifactProjectionPlanner.cs` | Execution artifact planning and common key helpers | Preserve key parity |
| `ProcessArtifactProjectionLineageBuilder.cs` | Recovery-aware lineage and provenance | Preserve recovery behavior |
