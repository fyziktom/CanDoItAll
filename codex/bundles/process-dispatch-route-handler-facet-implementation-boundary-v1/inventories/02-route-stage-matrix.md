# Route Stage Matrix

| Stage | Handler target | Must preserve | Side effects |
| --- | --- | --- | --- |
| FreshRecoverySkip | `FreshRecoverySkipRouteHandler` | fresh in-progress grace skip | logging only |
| DatabaseRequirement | `DatabaseRequirementRouteHandler` | PostgreSQL runtime requirement block behavior | transition step with claim |
| UpstreamMaterialization | `UpstreamMaterializationRouteHandler` | missing upstream artifact materialization request | transition + journal + rerun request |
| StrandedArtifactRecovery | `StrandedArtifactRecoveryRouteHandler` | manager artifact recovery finalizer handoff | finalizer + transition |
| Subprocess | `SubprocessRouteHandler` | child run observe/start, capability gap, terminal mirror | subprocess run, transition, projection |
| StartTransition | `StartTransitionRouteHandler` | start transition and reload-on-failure | transition + candidate reload |
| Workflow | `WorkflowRouteHandler` | workflow observe/run and finalizer handoff | workflow coordinator + transition |
| DirectAgentExecution | `DirectAgentExecutionRouteHandler` | execute-until-settled and heartbeat check | agent execution |
| CompetingExecutionGuard | `CompetingExecutionGuardRouteHandler` | skip non-success transition if competing active run exists | query only/logging |
| RunClosedGuard | `RunClosedGuardRouteHandler` | skip completion if run closed while in flight | query only/logging |
| FinalizerTransition | `FinalizerTransitionRouteHandler` | direct-agent finalizer and transition application | finalizer + transition |
