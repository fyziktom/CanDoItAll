# Route Order Matrix

The implementation must preserve this exact order inside the claimed-step dispatch pipeline:

| Order | Route/stage | Current owner | New boundary candidate | Must preserve |
| --- | --- | --- | --- | --- |
| 1 | Fresh recovery grace skip | Dispatch.cs | FreshRecoverySkipRouteHandler | Return without claim side effects beyond finally release |
| 2 | Database requirement block | Dispatch.cs + PreExecutionGuard | DatabaseRequirementRouteHandler | Same target status, same transition suppression |
| 3 | Missing upstream materialization | Dispatch.cs + PreExecutionGuard | UpstreamMaterializationRouteHandler | Same block reason, journal, rerun request |
| 4 | Stranded artifact recovery | Dispatch.cs | StrandedArtifactRecoveryRouteHandler | Same finalizer path |
| 5 | Subprocess | Dispatch.cs | SubprocessRouteHandler | Same child run ensure, capability gap, terminal mapping |
| 6 | Start transition | Dispatch.cs | StartTransitionRouteHandler | Same reload behavior when transition fails but status is InProgress |
| 7 | Workflow | Dispatch.cs + coordinator | WorkflowRouteHandler | Same `TryRunOrObserveAsync` and finalizer path |
| 8 | Direct agent execution | Dispatch.cs + Execution.cs | DirectAgentRouteHandler | Same execution/finalizer handoff |
| 9 | Competing execution guard | Dispatch.cs | CompetingExecutionGuard | Same skip behavior |
| 10 | Run closed guard | Dispatch.cs | RunClosedGuard | Same skip behavior |
| 11 | Finalizer and transition | Dispatch.cs + finalizer | DirectFinalizationRouteHandler | Same finalized transition application |
