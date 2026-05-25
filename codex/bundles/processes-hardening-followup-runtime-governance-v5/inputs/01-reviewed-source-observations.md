# Reviewed Source Observations

## Branch and Commit

- User mentioned: `process-hardening`
- GitHub branch found: `processes-hardening`
- Observed head: `474708e7a09d85a90d9541946e1e0e3dd964ec18`
- Commit message: `phase4`

## Positive Fixes Observed

1. `ProcessStepOperation`, `ProcessStepTargetScope`, and `ProcessStepOperationContract` now exist in `ProcessRunAutomationDispatchService.ExecutionMetadata.cs`.
2. Execution metadata now includes:
   - `agentProcessStepAllowedOperations`
   - `agentProcessStepTargetScope`
   - `agentProcessStepAllowsProductMutation`
3. `ExecutionInvocationMetadata.GroundPromptExternalTargetAliases` now avoids auto-promoting prompt-grounded aliases to writable when process boundary metadata disallows product mutation.
4. `DefaultAgentToolInvocationPolicy` blocks external target product mutation and managed output product mutation when the process step disallows product mutation.
5. The AgentFramework audit scope now carries `ProcessAllowsProductMutation`.
6. `RecordArtifactAsync` now passes the newly created artifact into downstream reactivation, so the materialized artifact can be considered before `SaveChanges`.
7. Artifact records now appear to carry `ProjectionLineageJson`.
8. Artifact validation now reads storage-backed content through `WorkspaceProcessArtifactContentReader`.
9. Finalizer routing now tracks artifact failure ownership and is more careful before routing missing artifacts to negative branch outcomes.
10. Linting exists and is integrated into publish/run-start paths through `ProcessDefinitionLintMode`.

## Remaining High-Risk Area

The implementation is no longer just prompt-hardening. However, several runtime concepts are still text-derived, file-system-specific, heuristic, or advisory by default. The next phase should convert these into first-class persisted contracts and runtime invariants.
