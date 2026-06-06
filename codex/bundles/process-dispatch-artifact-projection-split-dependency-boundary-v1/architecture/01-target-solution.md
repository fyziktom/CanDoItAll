# Target Boundary

- `ProcessRunAutomationDispatchService.ProjectExecutionArtifactsAsync` remains the dispatcher-owned entrypoint and only builds projection context plus orchestration dependencies.
- `ProcessArtifactProjectionOrchestrator` owns the projection source-family sequence.
- `IProcessArtifactProjectionSourceCoordinator` is internal and module-local.
- Source-family coordinators are top-level internal classes that depend on `IProcessArtifactProjectionHost`, not on `ProcessRunAutomationDispatchService`.
- The private dispatcher adapter is isolated in `ProcessRunAutomationDispatchService.ArtifactProjectionHost.cs`.
- Existing helper behavior remains in module-local partials and is not promoted into Process Core.
