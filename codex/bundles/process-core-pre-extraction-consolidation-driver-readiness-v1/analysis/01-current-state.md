# Current State Review

## Previous bundle status

The last bundle `process-core-contract-candidate-driver-readiness-prep-v1` completed successfully. Its execution report closes SB001-SB033, records build/unit/focused integration proof, and keeps Process Core and production driver APIs out of scope.

## Positive findings

- Route execution now uses route candidates and route DTOs rather than exposing full dispatcher nested models through route-facing services.
- Direct-agent execution now uses `ProcessDispatchDirectAgentExecutionInput`.
- `ProcessRouteExecutionOutcome` now exposes `ProcessRouteExecutionRunSnapshot` instead of the full execution detail in route-facing code.
- Projection context no longer carries full execution detail; projection observation facts are separate.
- Artifact projection, validation, and satisfaction now share `ProcessArtifactExpectationSnapshot`.
- The previous bundle added documentation-only driver readiness lanes and a permission model.

## Remaining blockers before Process Core

### 1. Route model source payloads are still a bridge

`ProcessRouteCandidate`, `ProcessRouteDispatchClaim`, and `ProcessRouteExecutionOutcome` still carry source marker interfaces so adapter code can recover dispatcher-owned payloads. This is acceptable at the module boundary but not a clean Core boundary.

### 2. Finalizer still needs a compatibility adapter

`ProcessDispatchFinalizerAdapter` still converts route input DTOs back to dispatcher-owned finalizer context and transition application. This must remain application-local, but the pure finalizer intent DTOs can be stabilized.

### 3. Hydration is still application-heavy

`ProcessDispatchCandidateHydrationService` still owns EF readback, workspace scope, execution-run lookup, recovery directive lookup, direct-agent binding, project-structure access mutation, and cooperation metadata. This should stay out of Core, but its pure read-model outputs and side-effect collaborators should be clearer.

### 4. Subprocess runtime is still mixed

`ProcessDispatchSubprocessRuntimeService` now exists, but subprocess orchestration and subprocess artifact projection persistence are still close together. This is application-local, but it needs a stronger boundary before Core.

### 5. Driver readiness is still documentation-only

This is correct. The next implementation should prepare verification vocabulary and permission test fixtures only, not production driver APIs.

## Recommended decision

Do not create Process Core in this bundle. Instead, run one broader pre-extraction consolidation pass across route source payloads, finalizer intent DTOs, hydration collaborators, subprocess projection persistence, artifact rule candidates, and driver-readiness proof.
