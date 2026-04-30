# Current-State Audit

## Verdict

The snapshot does not support the pasted Codex completion report. Some round-4 claims may have been implemented in another workspace, but they are not present in the uploaded ZIP.

The repository has useful foundations: process automation has structured output contracts, the process workspace UI exposes run/step/outbox/execution visibility, technical execution runs store tool receipts and approvals, and manual rerun exists. However, the current snapshot still has major safety and operability gaps.

## P0: committed provider credential remains

Evidence:

- `src/CanDoItAll.Web/appsettings.json:33` contains a value that matches an OpenAI API key pattern.
- No raw secret is repeated in this bundle.
- `SecretScanningTests.cs` was not found in the actual snapshot.

Impact:

- Any previous exposure should be considered compromised.
- Secret scanning/report claims cannot be trusted until validated by tests and scripts committed in the repository.

Required fix:

- Remove the secret value from all committed config/runtime payloads.
- Rotate/revoke the exposed key outside the repository.
- Add tests/scripts that scan tracked files and fail on provider key patterns.
- Ensure reports never echo secret values.

## Codex report mismatch

The pasted report claims files/classes/tests that were not found in the uploaded snapshot:

- `01-execution-report.md`
- `SecretScanningTests.cs`
- `AgentRecoveryModels.cs`
- `AgentRecoveryModelsTests.cs`
- `AgentReworkPacket`
- `ProofFingerprint`
- `RecoveryLedger`

Required fix:

- Add a snapshot integrity gate that verifies claimed files, tests, and reports exist before reporting success.

## Structured output and finalization

Positive evidence:

- `MafAgentRuntime.Session.cs` applies `ChatResponseFormat.ForJsonSchema(...)` when a structured output contract is supplied.
- `ProcessRunAutomationDispatchService.Execution.cs` invokes process automation with `ProcessStepOutcomeStructuredOutputContract`.
- `AgentStructuredOutputContract` exists and rejects unsuitable top-level structured output types.

Gaps:

- `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs:131-142` and `:150-160` continue pending approval runs with `structuredOutput: null`, so approval continuations can lose the schema constraint.
- The actual snapshot did not show required finalizer enforcement in `AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`.
- Assistant messages are persisted from `runtimeResponse.ResponseText` before any process-aware structured outcome finalization in the core execution service.

Required fix:

- Preserve structured output contract across all continuation paths.
- Add finalizer policies and exact-once finalizer enforcement for governed process outputs and other critical outputs.
- Validate and finalize machine output before persisting assistant transcript and before marking the technical run completed.

## Tool governance

Evidence:

- `MafAgentRuntime.AgentFactory.cs` uses function-call middleware for policy, but catches `InvalidOperationException` and `NotSupportedException` as policy exceptions.
- `MafAgentRuntime.Capabilities.Tools.cs:214` still has `IsBuiltInToolEnabled(...) => true`, which ignores enabled/disabled configuration.
- `MafAgentRuntime.cs:612-620` recognizes only workspace tools as mutation tools.
- `MafAgentRuntime.ProcessTools.cs:57-137` exposes process mutation tools as plain tools.

Gaps:

- Real tool failures may be reported as policy blocks.
- Disabled built-in tools may still be exposed.
- `processes_*` tools that mutate definitions, runs, assignments, artifacts, and transitions may be treated as read-like.
- Process mutation tools are not approval-wrapped in the actual snapshot.

Required fix:

- Use a dedicated `AgentToolPolicyBlockedException`.
- Respect tool enabled/disabled configuration.
- Classify all process mutation tools as mutation tools.
- Approval-wrap or deny process mutation tools unless the active policy permits them.
- Deny unknown `processes_*` tools by default.

## Agent failure, retry, and rework

Evidence:

- `ProcessRunAutomationDispatchService.Execution.cs` retries the current step, not the whole process.
- Recovery attempts usually create a fresh chat session (`automationChatSessionId = null`) to avoid stale context.
- Successful tool carry-forward is tracked as a set of tool names.
- `BuildRecoveryDirective(...)` creates a text-only recovery directive.
- Manual rerun exists via `ProcessesService.Runtime.Rerun.cs`, but it builds a text directive from operator reason, missing artifacts, blocked reason, and recent decisions.

Gaps:

- There is no typed `AgentRecoveryDecision`.
- There is no typed `AgentReworkPacket`.
- There is no proof fingerprint model that determines whether prior build/test/browser evidence is still valid.
- QA rejection/rework is not modeled as a typed, targeted repair flow.
- Retry loop control lacks durable ledger/backoff/loop detection beyond attempt limits and some repeated tool heuristics.

Required fix:

- Separate `FormatRepair`, `FreshStepRetry`, and `ReworkContinuation`.
- Generate typed rework packets for QA/build/test/browser/tool failures.
- Carry only validated context forward.
- Reuse proof receipts only by fingerprint, not by tool name.

## UI/process operability

Positive evidence:

- `ProcessWorkspaceRunsTab.razor` exposes Launch, Activity, Execution, Coordination, and Evidence tabs.
- `ProcessWorkspaceRunsLifecycleSection.razor` shows step status, health, metrics, branch outcome, and manual rerun button.
- `ProcessWorkspaceRunsExecutionSection.razor` shows outbox records, execution runs, approvals/checkpoints, and tool receipts.

Gaps:

- Manual rerun uses a fixed reason from UI; there is no operator rework form.
- Pending tool approvals are displayed, but the process UI does not provide direct approve/reject controls for them.
- Escalations are represented mainly as blocked/refused/failed step transitions and decision/conformance records, not as a first-class escalation queue.
- Operators cannot assign escalation owners, set severity/SLA, mark resolution, request changes, or generate a typed rework packet from an escalation.
- UI lacks attempt comparison, proof invalidation display, retry ledger, finalizer/structured-output validation status, and dead-letter recovery actions.

Required fix:

- Add an operator control plane for escalations, approvals, rework, retry, evidence, and run health.

## Testability and maintainability

Gaps:

- `ProcessRunAutomationDispatchService` is split into partials but still owns too many responsibilities.
- `MafAgentRuntime` and its partials mix provider setup, policy, tool composition, retry guidance, and runtime behavior.
- Several tests use reflection against private recovery-directive methods, which makes behavior brittle and hard to refactor.

Required fix:

- Extract focused services with contracts and unit tests.
- Favor behavior-level tests over string-only tests.
