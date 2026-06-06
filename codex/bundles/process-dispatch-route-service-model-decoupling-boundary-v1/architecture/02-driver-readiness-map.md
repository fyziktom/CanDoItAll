# Documentation-only Driver Readiness Map

This is not a production driver API.

| Route stage | Future driver relevance | Evidence family vocabulary | Current action |
| --- | --- | --- | --- |
| DatabaseRequirement | Runtime environment validation | RuntimePreconditionEvidence | Document only |
| UpstreamMaterialization | Artifact/materialization helper drivers | ArtifactMaterializationIntent | Document only |
| StrandedArtifactRecovery | Recovery/repair drivers | RecoveryProjectionEvidence | Document only |
| Subprocess | Delegated process drivers | DelegatedProcessOutcomeEvidence | Document only |
| StartTransition | Process lifecycle drivers | LifecycleTransitionIntent | Document only |
| Workflow | Workflow executor drivers | WorkflowExecutionEvidence | Document only |
| DirectAgentExecution | Agent/tool driver orchestration | AgentExecutionEvidence | Document only |
| CompetingExecutionGuard | Concurrency diagnostics | ConcurrencyGuardEvidence | Document only |
| RunClosedGuard | Lifecycle safety diagnostics | TerminalRunGuardEvidence | Document only |
| FinalizerTransition | Completion/evidence finalization | FinalizationEvidence | Document only |

Forbidden in this bundle:
- `IProcessDriverPack`
- `IProcessDriverRegistry`
- `ProcessDriverRegistry`
- production `DriverPack` folders/classes
- DI registration of process drivers
