# Current-State Review

## What Codex Improved

1. **Process operation constants exist.** `ProcessOperationContractNames` now provides string constants for operations such as `ReadProcessContext`, `ReadProjectStructure`, `WriteManagedProcessArtifacts`, `MutateProductTarget`, `RunValidation`, `LaunchRuntime`, `CaptureRuntimeProof`, `ExecuteExternalAction`, `RecoverArtifactsOnly`, and `EscalateOrDecide`.
2. **A tool contract catalog exists.** Workspace, browser, API, project-structure, and process tool names have a shared catalog surface.
3. **Usage observations exist.** `AgentRuntimeResponse` now carries `UsageObservations`, and runtime failure can carry `AgentRuntimeUsageException.UsageObservations`.
4. **Process cost aggregation is ledger-first when observations exist.** `ResolveProcessRunActualCost` checks execution-run details and prefers provider usage observations before falling back to legacy metrics.
5. **Workflow LLM usage consumes observations when present.** `MafWorkflowLlmComponentInvoker` summarizes usage from runtime observations before falling back to runtime response counts.
6. **SB08 contains five domain-distinct app scenarios.** Tetris, Expense Tracker, Plant Watering Planner, Study Kanban Flashcards, and Recipe Pantry Planner are present.

## Remaining Critical Gaps

### P0-01: Governed process operation enforcement can still fail open

`ProcessToolOperationAuthorizer` returns no denial when a governed process run has no normalized allowed operations. That means the runtime can fall through to other policy checks instead of treating missing contract as a process-definition defect.

A related normalizer behavior returns no issue when both allowed operations and target scope are absent. That may be acceptable for legacy authoring, but it is unsafe for `GovernedLive` execution unless strict mode separately blocks the run.

### P0-02: Tool registry is still split and default-read can hide missing metadata

`ToolContractCatalog` names many workspace/browser tools, including `workspace_command_run`, browser interaction tools, browser evidence tools, and local launch helpers. The explicit `RegisteredTools` table in `AgentToolInvocationPolicyMetadata` is narrower. Unregistered names that are not `project_structure_`, `processes_`, provider-native, or MCP-prefixed fall back to `ToolInvocationClassification.Read`.

This means a dangerous or side-effectful known tool can accidentally avoid explicit metadata and operation requirements when the mapping is incomplete.

### P0-03: SB08 is not a real agent-driven process E2E proof

The SB08 harness starts a process run and uploads project-structure input, but then it:

- generates the scenario app from PowerShell helper functions in the proof script,
- starts the generated app host directly,
- validates via Chrome CDP directly,
- transitions process steps manually,
- sets `suppressAutomationDispatch = $true`,
- records `executionRuns = @()`, and
- records `canDoItAllProviderUsageObserved = false`.

That is useful as a browser regression and process API writeback harness. It does **not** prove the full automation path: manager → process dispatch → role/agent execution → tool calls → artifact production → validation → cost ledger.

### P0-04: Proof gate accepted a bypassed critical path

The final red-team report and execution report mark the bundle as complete and SB08 as passed, while the SB08 proof itself says provider usage was unavailable because no CanDoItAll provider execution runs were generated. A final proof-quality gate must have failed this or explicitly classified it as a fixture-only proof.

### P1-01: Provider usage normalization is not complete enough for OpenAI billing reconciliation

The runtime reads generic `InputTokenCount`, `CachedInputTokenCount`, and `OutputTokenCount`, stores raw usage JSON, but sets `ReasoningTokens = 0` and `TotalTokens = input + output`. OpenAI Responses usage can include nested cached-token and reasoning-token details plus a provider `total_tokens`; usage can also be `null` in some response states.

Internal totals can therefore diverge from provider billing unless the raw usage payload is normalized provider-specifically and reconciliation reports are added.

### P1-02: Finalizer paths still create zero-token aggregate runtime metrics

Required finalizer short-circuit and finalizer recovery responses still construct `AgentRuntimeResponse` with `InputTokens: 0` and `OutputTokens: 0`, relying on `UsageObservations` for truth. This is acceptable only if every consumer uses observations. Any UI, metric, report, or legacy aggregation that reads `AgentRunMetric` or runtime response token fields directly can still undercount.

### P1-03: Policy and process dispatch are still too heuristic-heavy

`AgentToolInvocationPolicy` remains a large mixed-responsibility policy object. `ProcessRunAutomationDispatchService.GovernedRules` contains many regex/text-signal heuristics for tool requirements, browser proof, step types, and artifact acceptance. This is workable for the current Blazor flow but risky before more process families are added.

### P1-04: Cost proof is internal-only

V1 explicitly says OpenAI external billing reconciliation was pending. That is honest, but it means the user’s original billing mismatch concern is not fully closed.

## Reopen Decision

V1 should be considered **implemented but reopened for P0/P1 hardening**. The next Codex pass must not add new process families before SB01-SB05 of this follow-up pass.
