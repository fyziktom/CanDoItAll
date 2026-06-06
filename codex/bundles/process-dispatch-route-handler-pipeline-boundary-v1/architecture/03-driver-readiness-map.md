# Documentation-only Driver Readiness Map

Do not implement production driver APIs in this bundle.

| Future concept | Current route-stage meaning | Current action |
| --- | --- | --- |
| `EnvironmentRequirementEvidence` | PostgreSQL requirement block before agent automation | Document only |
| `UpstreamArtifactMaterializationIntent` | Missing upstream artifact rerun request | Document only |
| `StrandedRecoveryEvidence` | Manager artifact recovery finalizer route | Document only |
| `DelegatedProcessEvidence` | Subprocess route completion/parent projection | Document only |
| `WorkflowExecutionEvidence` | Workflow route handled step execution | Document only |
| `DirectAgentExecutionEvidence` | Direct-agent execution and finalizer handoff | Document only |
| `ConcurrentExecutionGuardEvidence` | Competing execution guard prevents stale transition | Document only |
| `RunClosedGuardEvidence` | Terminal run/step prevents finalizer transition | Document only |
| `FinalizerTransitionEvidence` | Finalized completion applied to step | Document only |

These names are vocabulary for analysis only. They must not appear as production driver interfaces/classes.
