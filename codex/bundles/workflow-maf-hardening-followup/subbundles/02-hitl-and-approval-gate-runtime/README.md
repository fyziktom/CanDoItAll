# 02-hitl-and-approval-gate-runtime

## Objective

Replace coarse graph-level waiting and missing product approval runtime with execution-position-aware HITL and approval request handling.

## Current problem

`WorkflowRuntimeManager.StartAsync` immediately returns `WaitingForInput` when any `HumanInput` node exists in the workflow graph. This blocks workflows even when the human node is not reached by routing. Approval-required executors are now modelled, but no concrete product approval gate is registered in reviewed Agent Framework registrations.

## Exact source references

- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`
- `src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs`
- `src/CanDoItAll.AgentFramework.Models/Workflows/*`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`
- `src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`
- `src/CanDoItAll.Modules.AgentFramework/Services/AgentFrameworkModuleServiceCollectionExtensions.cs`
- `tests/CanDoItAll.Tests.Unit/WorkflowExecutorPolicyObservabilityTests.cs`
- `tests/CanDoItAll.Tests.Integration/WorkflowApiIntegrationTests.cs`

## Implementation steps

1. Add failing-first tests:
   - Workflow contains a `HumanInput` node on an unselected route and should complete without waiting.
   - Workflow reaches a `HumanInput` node and should persist a pending external request.
   - Approval-required executor should create/require an approval request path and execute only after approval.
2. Remove or narrow the preemptive `FirstOrDefault(node.Kind == HumanInput)` shortcut in `WorkflowRuntimeManager`.
3. Model human input as an execution step:
   - either compile a human node to a MAF request/response pattern,
   - or create a CanDoItAll request event from the node executor only when the node is actually reached.
4. Implement a concrete `IWorkflowExecutorApprovalGate` for product runtime:
   - persists `WorkflowExternalRequestRecord`,
   - redacts settings,
   - supports approve/deny/timeout,
   - can be driven by UI/API tests.
5. Register the approval gate in both hosting and module composition where appropriate.
6. Ensure preview simulation can explicitly bypass/auto-answer approval only under `WorkflowPreviewSimulationPlan`.

## Do not do

- Do not auto-approve live external writes.
- Do not run live Gmail/Office365/Docker as proof.
- Do not block the whole graph merely because a human node exists.

## Acceptance checklist

- Unreachable human nodes do not pause the workflow.
- Reached human nodes pause with a pending request.
- Approval-required executors can complete after explicit approval and cannot execute after denial.
- Docker executors remain safe by default.
- Tests prove no external effect in default proof.

## Proof required

- Failing-first test transcript.
- Passing unit and integration test transcript.
- Registration/source assertion showing concrete approval gate wiring.
