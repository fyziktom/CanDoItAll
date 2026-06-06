# SB018 Critical Gate Manifest

- Gate: run-closed and claim-held guard service.
- Result: closed.
- `ProcessDispatchRunClosureGuardService` owns run-closed checks used by route guard paths.
- `ProcessDispatchStepTransitionService` centralizes claim-held transition calls used by route services and subprocess runtime.
