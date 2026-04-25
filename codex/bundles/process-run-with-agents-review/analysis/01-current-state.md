# Current State

## What The Implementation Can Do

- Launch planning is UI-backed through `ProcessWorkspaceRunsLaunchSection.razor`: users can create launch plans, select role candidates, submit for approval, approve, provision, and execute ready launches.
- Runtime run selection is UI-backed through `ProcessWorkspaceRunsLifecycleSection.razor`: users can select a durable run and see run status, blocked step count, estimated cost, and actual cost.
- Runtime observation is partially UI-backed:
  - `ProcessWorkspaceRunsActiveSection.razor` shows active AgentFramework execution summaries while runs are active.
  - `ProcessWorkspaceRunsExecutionSection.razor` shows execution runs, raw/governed status badges, prompt/result summaries, approvals, checkpoints, tool receipts, and produced execution artifacts.
  - `ProcessWorkspaceRunsArtifactsSection.razor` shows process artifacts, work briefs, decisions, and conformance observations.
  - `ProcessWorkspace.LiveRefresh.cs` refreshes selected process runtime state every 4 seconds while active runs or launch plans exist.
- Runtime interaction is partially UI-backed:
  - Step status buttons can start, complete, block, request approval, refuse, or fail a step.
  - Runtime canvas selection can apply the same step status actions and prepare artifact capture.
  - Users can record manual artifacts and send direct role messages.
- Backend process automation can complete the deterministic process mock path:
  - `ProcessMockAgentRuntimeIntegrationTests.Process_mock_calculator_process_completes_end_to_end_through_durable_outbox_dispatch` proves launch plan execution, durable outbox dispatch, mock-agent execution, QA rejection, repair, QA approval, artifact records, branch routing, and outbox completion.

## What Is Not Yet Proven

- No browser or Playwright proof launches the deterministic agent process through the UI and observes it to completion.
- No UI proof covers missing required artifacts, dead-lettered outbox records, agent crash recovery, stale execution recovery, context loss, or manual operator retry.
- No first-class UI model exposes process outbox records, outbox attempts, last errors, lease state, next retry time, or dead-letter status.
- No first-class UI model exposes a required artifact expectation ledger showing expected, satisfied, auto-projectable, missing, or failed-to-project artifacts per step.
- No first-class UI control reruns a failed or blocked agent-owned step with a structured recovery directive.

## Artifact Transfer Current State

AgentFramework artifacts are transferred into process evidence by `ProcessRunAutomationDispatchService.ProjectExecutionArtifactsAsync`.

- Generic execution artifacts are read from workspace-relative paths, placed through `IStoragePlacementService`, and recorded as `ProcessArtifactRecord`.
- Duplicate projection is guarded by `ExternalReferenceKey`.
- Missing or unreadable generic execution artifact files are skipped with logs.
- Deterministic process mock artifacts are stricter: missing, ambiguous, or unmatched mock artifact projection throws an exception.
- Response text and some decision artifacts can be auto-projected when the expected artifact contract allows it.
- The UI displays final artifact records and execution artifacts but does not display the expectation-to-record satisfaction matrix.

## Missing Artifact Current State

Missing required artifacts are detected by dispatcher logic that compares expected artifacts to recorded or auto-satisfiable artifacts.

- Successful but incomplete runs can be retried before max attempts.
- After retries are exhausted, a successful raw execution with missing required artifacts resolves to `Blocked` if the agent declared completion, or `Blocked` before implicit completion.
- Missing required artifact details flow into transition reason and blocked reason, but the UI does not expose the full expected/missing contract as a guided repair workflow.
- If artifact projection itself throws, the dispatcher catches the exception and attempts to move the step to `Failed`.

## Agent Crash And Context Loss Current State

- `AgentFrameworkExecutionRecoveryService` marks interrupted AgentFramework runs as `Failed` and `Cancelled` on startup when no resumable approval exists.
- `ProcessRunRecoveryWorker` scans active non-terminal process runs with ready, waiting, or in-progress agent-owned steps and calls `ProcessRunAutomationDispatchService.DispatchAsync`.
- The dispatcher can recover a terminal automation execution for an in-progress step when the execution is completed or failed and not cancelled.
- The dispatcher starts recovery attempts on a fresh chat session after incomplete or recoverable failed attempts.
- Recovery prompts include detailed retry guidance for known failure patterns.
- There is no explicit UI state showing whether a retry is a continuation, fresh chat retry, provider repair retry, crash recovery retry, or manual rerun.
- There is no durable recovery package that summarizes prior attempt artifacts, last errors, missing outputs, and exact instructions for the next attempt as a user-visible object.

## UI Feasibility Answer

The current UI can start and observe the happy path in principle because launch planning, run history, active execution summaries, execution details, evidence, and manual step actions exist. It is not yet proven or operator-complete for this workflow. A user can interact with a process run, but cannot reliably operate the failure and recovery paths that make agent-driven work safe: missing artifacts, stale/crashed agents, outbox dead letters, repeated retries, and context-loss recovery are mostly backend/log concepts today.
