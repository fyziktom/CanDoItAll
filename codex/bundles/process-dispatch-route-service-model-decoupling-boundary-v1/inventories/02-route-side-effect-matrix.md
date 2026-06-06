# Route Side-effect Matrix

| Stage | Side-effect kind | Allowed owner after this bundle |
| --- | --- | --- |
| FreshRecoverySkip | None/logging | route handler + logging service |
| DatabaseRequirement | transition/block | database requirement route service |
| UpstreamMaterialization | transition + journal + rerun request | upstream materialization route service |
| StrandedArtifactRecovery | recovery + finalizer + transition | recovery route service |
| Subprocess | subprocess lifecycle + projection | subprocess route service |
| StartTransition | transition + candidate reload | start transition route service |
| Workflow | workflow run/observe + finalizer | workflow route service |
| DirectAgentExecution | agent execution loop | direct-agent route service |
| CompetingExecutionGuard | execution query | guard route service |
| RunClosedGuard | run status query | guard route service |
| FinalizerTransition | finalizer + transition | finalizer route service |

Every side effect must remain explicit. Do not move side effects into a method named like a pure rule.
