# Structured Input

## Request Summary

- Continue the `maf-processes-refactor` dispatcher decomposition with smaller safe isolation steps.
- Preserve all existing process artifact projection behavior and source-family order.
- Do not start `CanDoItAll.Processes.Core` or production process-driver APIs in this bundle.
- Split the remaining all-facet dispatcher-backed projection implementation into smaller module-local implementations.
- Keep UI, responsive proof, EF movement, public contract movement, and DB migration out of scope.

## Target Boundary

- Current residual coupling: `ProcessRunAutomationDispatchService.ProcessArtifactProjectionServices`.
- Target outcome: focused facet implementations or small adapters consumed only by coordinators that need them.
