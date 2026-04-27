# MAF runtime stabilization

This document records the runtime invariants added for the MAF stabilization bundle.

## Structured output

- `ExecutionRunRequest.StructuredOutput` is persisted on `ExecutionRunRecord`.
- Pending-approval checkpoints include the structured-output contract key, output type, schema name, and schema description.
- Manual and auto-approved continuations restore the original contract before calling the runtime.
- Governed process-step runs fail if a continuation cannot resolve the stored contract.
- Successful governed runs validate the raw model response through `DefaultAgentOutputValidatorRegistry` before completion.
- Validation logs include the contract key and raw output hash; full raw payloads are not logged.

## Tool policy

`DefaultAgentToolInvocationPolicy` evaluates function calls before the tool body runs.

Policy inputs include:

- agent id/name,
- tool name,
- redacted arguments,
- known-tool membership,
- tool classification,
- auto-approval state,
- approval-wrapper availability,
- execution/process/step ids.

Policy decisions are explicit: `Allow`, `RequireApproval`, `Deny`, `SanitizeResult`, or `SkipExecution`. The current middleware allows normal reads, requires an approval wrapper for mutations when auto-approval is off, denies unknown tools, and blocks the fourth identical mutation or validation signature in a single run.

Sensitive argument names containing `api_key`, `apikey`, `authorization`, `credential`, `header`, `password`, `secret`, or `token` are redacted before signatures or logs are created.

## Provider gates

Provider features are resolved centrally through `ProviderProfileService.ResolveFeatureMatrix`.

Important flags:

- `SupportsStructuredOutput`
- `SupportsToolApprovalWrappers`
- `SupportsLocalMcpBridge`
- `SupportsServiceManagedHistory`
- `SupportsVision`
- `SupportsCompaction`

Structured output is enforced only for Responses-backed OpenAI or Azure OpenAI providers. Unsupported providers fail before execution starts instead of silently ignoring a machine-critical contract.

## Finalizers

`AgentFinalizerPolicy` defines whether a decision requires an exact-once typed finalizer tool. `DefaultAgentFinalizerValidator` validates captured finalizer invocations through the same output validator registry used for structured responses.

For `ProcessStepOutcomeResult`, the MAF runtime registers `submit_process_step_outcome` with a typed `ProcessStepOutcomeResult` argument. Process-step runs default to shadow mode: finalizer calls are captured, validated, traced, logged, and compared with the structured response without replacing the structured response as source of truth. Setting execution metadata `agentFinalizerMode` to `required` makes the finalizer authoritative and causes missing, repeated, or invalid finalizer calls to fail the run before success persistence.

Failure cases:

- required finalizer missing,
- required finalizer called more than once,
- malformed finalizer arguments,
- no registered validator for the finalizer output type.

Assistant text is display-only when a finalizer is required.

## New-agent checklist

- Choose a provider profile whose feature matrix supports the required capabilities.
- Use structured output for machine-critical process, approval, branch, review, or tool decisions.
- Attach only tools the agent is allowed to use; set built-in tool `enabled` to `false` when disabled.
- Require approval wrappers for write/destructive tools unless the run is explicitly auto-approved.
- Use finalizer policy for exact-once critical decisions.
- Keep markdown summaries display-only.
- Validate with focused unit tests plus at least one integration path using fake/mock runtime behavior.
- Run live-provider validation behind environment guards when credentials and host dependencies are available.

## Validation commands

Focused validation used by this bundle:

```powershell
dotnet build CanDoItAll.slnx --no-restore -v:minimal
dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore -v:minimal --filter "FullyQualifiedName~AgentFinalizerPolicyTests|FullyQualifiedName~AgentOutputContractTests|FullyQualifiedName~AgentToolInvocationPolicyTests|FullyQualifiedName~ProviderFeatureMatrixTests"
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore -v:minimal --filter "FullyQualifiedName~AgentFrameworkExecutionRunTrackingIntegrationTests"
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore -v:minimal --filter "FullyQualifiedName~MafAgentRuntimeTests"
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore -v:minimal --filter "FullyQualifiedName~ProcessMockAgentRuntimeIntegrationTests"
dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore -v:minimal --filter "FullyQualifiedName~ProcessRunAutomationDispatchServiceTests"
$env:CANDOITALL_RUN_LIVE_AGENT_VALIDATION='true'; dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --no-restore -v:minimal --filter "FullyQualifiedName~LiveSpecialistAgentScenarioIntegrationTests"
```
