# Current State Analysis

The branch is ready for another incremental isolation bundle, but not yet for Process Core.

## What is already separated

- `ProcessDispatchCandidateHeaderSelector`
- `ProcessDispatchCandidateHydrationLoader`
- `ProcessDispatchArtifactInputAssembler`
- `ProcessDispatchBranchDependencyContext`
- `ProcessDispatchAssignmentRouteHelper`
- `ProcessDispatchTechnicalAgentBindingCoordinator`
- `ProcessDispatchRecoveryQueryHelper`
- `ProcessDispatchCandidateAssemblyContext`
- `ProcessDispatchCandidateFactory`
- `ProcessDispatchCooperationMetadataResolver`
- `ProcessDispatchPreExecutionGuardHandler`
- `ProcessMissingUpstreamArtifactMaterialization*`
- artifact projection/write helpers
- artifact validation rule helpers
- tool validation/recovery helpers
- step completion finalizer helper partials

## What remains too large

`ProcessRunAutomationDispatchService.Dispatch.cs` remains a central orchestration file. After recent extraction, the next cohesive seam is subprocess runtime and subprocess artifact projection.

## Why subprocess next

The subprocess branch is a bounded domain inside dispatch. It has clear lifecycle semantics and side effects that can be isolated without introducing Process Core:

- parent step start/block/final transition
- child subprocess run observe/start
- capability gap observation
- child-to-parent artifact projection
- projection gap diagnostics
- finalizer context handoff
