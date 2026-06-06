# Route Service Boundary Inventory

## Current broad adapter

`ProcessDispatchRouteServices` currently implements all route-facet interfaces:

- `IProcessDispatchDatabaseRequirementRouteFacet`
- `IProcessDispatchUpstreamMaterializationRouteFacet`
- `IProcessDispatchRecoveryRouteFacet`
- `IProcessDispatchSubprocessRouteFacet`
- `IProcessDispatchStartTransitionRouteFacet`
- `IProcessDispatchWorkflowRouteFacet`
- `IProcessDispatchDirectAgentRouteFacet`
- `IProcessDispatchGuardRouteFacet`
- `IProcessDispatchFinalizerRouteFacet`

This is the next boundary to split.

## Route model aliases to remove from route-facing files

Current aliases to replace outside explicit adapter files:

- `DispatchCandidate = ProcessRunAutomationDispatchService.DispatchCandidate`
- `DispatchExecutionOutcome = ProcessRunAutomationDispatchService.DispatchExecutionOutcome`
- `ProcessStepDispatchClaim = ProcessRunAutomationDispatchService.ProcessStepDispatchClaim`

## Route stage order to preserve

1. FreshRecoverySkip
2. DatabaseRequirement
3. UpstreamMaterialization
4. StrandedArtifactRecovery
5. Subprocess
6. StartTransition
7. Workflow
8. DirectAgentExecution
9. CompetingExecutionGuard
10. RunClosedGuard
11. FinalizerTransition
