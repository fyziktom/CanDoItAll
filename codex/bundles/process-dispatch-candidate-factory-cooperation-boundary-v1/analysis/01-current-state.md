# Current State Analysis

The previous bundle successfully moved candidate header selection and hydration readback into module-local helper boundaries. This is a meaningful step, but the candidate construction seam is not yet isolated.

## Completed boundary work

- Header selection is delegated to `ProcessDispatchCandidateHeaderSelector.SelectAsync`.
- Hydration readback is delegated to `ProcessDispatchCandidateHydrationLoader.LoadAsync`.
- Artifact input assembly is delegated to `ProcessDispatchArtifactInputAssembler`.
- Branch outcome / conditional dependency shaping is delegated to `ProcessDispatchBranchDependencyContext`.
- Assignment route facts are partially delegated to `ProcessDispatchAssignmentRouteHelper`.
- Technical-agent binding/access side effects are explicit in `ProcessDispatchTechnicalAgentBindingCoordinator`.
- Manual recovery directive and recoverable execution lookup are delegated to `ProcessDispatchRecoveryQueryHelper`.

## Remaining hotspot

`LoadDispatchCandidateAsync` still assembles different `DispatchCandidate` shapes inline:

- subprocess candidate
- workflow-backed candidate
- direct-agent candidate
- technical-agent bound candidate
- recovery/manual rerun candidate
- cooperation metadata candidate

It also still manually coordinates expected artifacts, prepared artifact inputs, branch context, workflow assignment, current assignment/current role, execution run query, binding result, access grant logging, recovery execution id, and cooperation metadata.

## Recommended next seam

Extract candidate construction/factory and cooperation metadata boundaries into module-local helpers. This should reduce direct object construction duplication and clarify future driver-readiness semantics without creating a Process Core or production driver API.
