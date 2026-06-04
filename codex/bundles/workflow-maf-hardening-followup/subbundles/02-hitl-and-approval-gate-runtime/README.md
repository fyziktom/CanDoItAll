# 02-hitl-and-approval-gate-runtime

## Status

- Status: `Completed`

## Objective

Replace coarse graph-level waiting and missing product approval runtime with execution-position-aware HITL and approval request handling.

## Covered Inputs

- R2: Replace graph-level preemptive `HumanInput` waiting with execution-position-aware handling.
- R3: Implement a product approval gate for approval-required workflow executors.
- R11: Keep live external effects disabled by default in proof.

## Prerequisites

- SB01 package/API baseline is completed or blocked with an accepted ADR.
- Workflow runtime source references still match the current repo.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Workflows/WorkflowExecutorModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `repo://src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `repo://tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs`
- `repo://tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`

## Scope

- Add failing-first and passing tests for unreachable human nodes, reached human nodes, and approval-required executors.
- Remove or narrow graph-level `HumanInput` preemption.
- Add concrete product approval-gate behavior and service registration.

## Dependency Impact

- SB03 event identity and SB04 checkpoint work depend on request and approval states being produced at the correct execution point.

## Validation Depth

- Unit and integration proof must include negative denial/timeout and positive approval/reached-HITL behavior.
- Critical proof requires failing-first, passing, source assertion, anti-stub, and downstream smoke transcripts.

## Implementation Steps

1. Add failing-first tests for unreachable `HumanInput`, reached `HumanInput`, and approval-required executor approval/denial.
2. Remove or narrow the `FirstOrDefault(node.Kind == HumanInput)` shortcut in `WorkflowRuntimeManager`.
3. Model human input as a reached execution step or MAF request/response flow.
4. Implement a concrete `IWorkflowExecutorApprovalGate` that persists redacted `WorkflowExternalRequestRecord` state.
5. Register the approval gate in product composition.
6. Keep preview simulation bypass/auto-answer behavior explicit and test-controlled.

## Do Not Do

- Do not auto-approve live external writes.
- Do not run live Gmail, Office365, Docker, or host-command proof.
- Do not block a whole graph merely because a human node exists somewhere.

## Acceptance Checklist

- Unreachable human nodes do not pause workflow execution.
- Reached human nodes pause with a pending request.
- Approval-required executors can complete after explicit approval and cannot execute after denial.
- Docker and external-write executors remain safe by default.

## Proof Required

- Failing-first test transcript.
- Passing unit and integration test transcript.
- Registration/source assertion showing concrete approval gate wiring.
- `bundle://proof/SB02/manifest.md` and `bundle://proof/SB02/semantic-invariants.md`.

## Browser Validation Logging

- Browser proof is not required unless approval/HITL UI surfaces are changed; API/component proof is required for runtime-visible state.

## Progression Gate

- Continue to SB03 only if execution-position HITL and approval decisions produce durable request/event state with no live external effects in default tests.
- Result: `Passed`. Proof is captured in `bundle://proof/SB02/manifest.md` and `bundle://proof/SB02/semantic-invariants.md`.

## Suggested Agent Prompt

Implement execution-position-aware HITL and approval gate runtime with failing-first tests, redacted persisted requests, and explicit denial/approval behavior.
