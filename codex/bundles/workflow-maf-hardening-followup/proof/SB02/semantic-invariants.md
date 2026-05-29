# SB02 semantic invariants

## SB02-HITL-EXECUTION-POSITION

- Invariant ID: `SB02-HITL-EXECUTION-POSITION`
- Source raw note: R2 requires replacing graph-level preemptive `HumanInput` waiting with execution-position-aware handling.
- Expected behavior: A workflow definition may contain a human-input node on an unreached branch without pausing. A pending human-input request is created only when workflow execution reaches that node.
- Disallowed shallow implementation: Checking `definition.Graph.Nodes.FirstOrDefault(node => node.Kind == WorkflowNodeKind.HumanInput)` before backend execution, or creating a request for a human node that routing skipped.
- Failing-first test: `bundle://proof/SB02/transcripts/failing-first-hitl-route-tests.txt` shows the old runtime paused on an unreached human-input branch and lacked prior node progress events.
- Passing test: `bundle://proof/SB02/transcripts/unit-hitl-approval-after-response-semantics.txt` shows unreached HITL completes with no pending requests and reached HITL waits with one pending request.
- API proof: `bundle://proof/SB02/transcripts/integration-workflow-api-hitl-approval-after-implementation.txt` shows route `api/workflows/test-runs` exposes the same route-position behavior.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-hitl-approval.txt` verifies the runtime manager no longer contains the graph-level HITL shortcut and the compiler/backend produce/capture pending request state at execution time.
- Red-team negative case: the automatic route in the new unit and API tests includes a human node in the graph but asserts `Completed` with no pending request.

## SB02-APPROVAL-GATE-RUNTIME

- Invariant ID: `SB02-APPROVAL-GATE-RUNTIME`
- Source raw note: R3 requires a concrete product approval gate for approval-required workflow executors, and R11 requires no live external effects in proof.
- Expected behavior: Approval-required executors do not execute without approval. Denied approvals stop before execution with redacted messages. The product gate creates a redacted `WorkflowExternalRequestRecord` for approval, and approval responses are explicit `approved` decisions.
- Disallowed shallow implementation: Auto-approving external-effect executors, logging raw secrets in approval payloads, or relying on tests that call live Gmail, Office365, Docker, or host commands.
- Failing-first test: existing denial/no-gate tests were kept and the new product gate test fails if no pending request is thrown before executor invocation.
- Passing test: `bundle://proof/SB02/transcripts/unit-hitl-approval-after-response-semantics.txt` shows explicit approval executes the fake executor, denial blocks execution, product approval creates a redacted pending request, approved persisted requests complete, denied persisted requests fail, and malformed approval responses are rejected.
- Production assertions: `bundle://proof/SB02/transcripts/source-assertions-hitl-approval.txt` verifies `WorkflowExternalRequestApprovalGate`, `WorkflowExternalRequestCaptureScope`, approval response semantics, and DI registration in both hosting paths.
- Red-team negative case: approval test payloads include raw token-like values and assert they do not appear in request JSON or denied summaries.
- Downstream dependency check: `bundle://proof/SB02/transcripts/component-workflows-page-smoke-after-hitl-approval.txt` and `bundle://proof/SB02/transcripts/solution-build-slnx-after-hitl-approval.txt` prove the workflow component slice and solution still build after the runtime changes.

- Changed source files: `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs`, `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExternalRequestRuntime.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowCompiler.cs`, `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafInProcessWorkflowExecutionBackend.cs`, and tests listed in `bundle://proof/SB02/manifest.md`.

## Production Behavior Artifact Matrix

| Artifact | Producer | Consumer | Lifecycle | Negative proof |
| --- | --- | --- | --- | --- |
| HITL external request state | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowRuntimeManager.cs` | `repo://src/CanDoItAll.Web/Api/WorkflowsApi.cs` | `bundle://proof/SB02/transcripts/integration-workflow-api-hitl-approval-after-implementation.txt` | `bundle://proof/SB02/transcripts/failing-first-hitl-route-tests.txt` |
| Approval request state | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExternalRequestRuntime.cs` | `repo://src/CanDoItAll.AgentFramework.Core/Workflows/WorkflowExecutorContracts.cs` | `bundle://proof/SB02/transcripts/unit-hitl-approval-after-response-semantics.txt` | `bundle://proof/SB02/transcripts/unit-hitl-approval-after-response-semantics.txt` |
