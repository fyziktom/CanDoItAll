# Token And Cost Accounting Audit

## Finding

The current implementation can plausibly undercount OpenAI usage compared with provider billing.

This is not a pricing-table-only issue. The larger issue is that the current process run cost is derived from persisted execution-run metrics, while the runtime has paths that can either skip usage capture or persist an estimated/zero metric after a provider interaction.

## Concrete Risk Classes

### TC-01: Finalizer short-circuit returns zero tokens

`MafAgentRuntime` can short-circuit a required finalizer result and construct `AgentRuntimeResponse` with zero input/output tokens. Required finalizer behavior is used for governed machine-critical process-step runs. These are exactly the runs whose process cost the user is inspecting.

Required repair:

- Preserve and aggregate usage from streaming updates before returning the short-circuited finalizer response.
- If MAF does not expose usage before short-circuit, create a usage observation with `UsageStatus = MissingAfterProviderActivity` and response/session identifiers.
- Add an adversarial test where a required finalizer is invoked after model output/tool activity and prove the usage is not zeroed.

### TC-02: Failed runs use prompt estimates or zero

The execution service failure path records either a prompt estimate or zero tokens. A failure can occur after provider streaming, tool call generation, finalizer validation, repeated tool invocation guard, serialization, or output validation. Those can all happen after billable model usage.

Required repair:

- Capture partial provider usage as soon as usage-bearing response metadata is available.
- Persist usage observations independently of final run success.
- Add a failing-first test where a mocked runtime reports usage and then fails; the ledger must retain usage.

### TC-03: Structured-output repair calls may be unlinked

The output repair service may call a model. If it does, it must produce a child usage observation linked to the parent execution run and process run. A repair call must not disappear inside validation logs.

Required repair:

- Inspect the concrete output repair service.
- Add context propagation: parent execution run id, process run id, process step id, workflow run id, source phase `structured-output-repair`.
- Add tests where repair succeeds and fails; both must record usage or usage-unavailable status.

### TC-04: Background/polling usage may be non-cumulative or partially hidden

The runtime accumulates streamed updates, then reads `response.Usage`. Codex must verify whether MAF's aggregated `AgentResponse` usage is cumulative across background polling. If not, each response/poll needs a separate usage observation.

Required repair:

- Store provider response id and continuation token per usage event.
- Add tests for multi-poll background response behavior.
- Reconcile `sum(usage.total_tokens)` against the final metric.

### TC-05: Workflow model calls are not necessarily part of process run cost

The Office365 workflow summarized an email before starting or informing a process. If workflow summarization uses a provider, it may not be linked to the process run's cost. The UI should distinguish workflow cost, process cost, and end-to-end intake-to-release cost.

Required repair:

- Define cost scopes: `WorkflowRun`, `ProcessRun`, `ExecutionRun`, and optional `EndToEndCorrelation`.
- Process run detail can show linked workflow usage only if correlation is explicit.
- Dashboard should avoid pretending a process run actual cost includes upstream workflow model calls unless it does.

### TC-06: Unknown usage must not be rounded away

OpenAI Responses objects expose usage fields when available, including input, cached input, output, reasoning, and total tokens. Some cancelled/background responses can have `usage: null`. The internal ledger must represent both facts: observed usage and missing usage.

Required repair:

- Add `UsageStatus` and `UsageCompleteness`.
- Add "known cost", "estimated unknown cost", and "unreconciled usage events" rather than one precise-looking number.

## Proposed New Contract

Create a durable `ProviderUsageObservation` or equivalent model with at least:

```text
Id
CreatedAtUtc
ProviderName
ProviderKind
Model
TransportKind
ProviderResponseId
ProviderRequestId
RuntimeSessionKey
ExecutionRunId
ProcessRunId
ProcessStepId
WorkflowRunId
WorkflowNodeId
CorrelationId
SourcePhase
UsageStatus
InputTokens
CachedInputTokens
OutputTokens
ReasoningTokens
TotalTokens
ToolCallCount
ProviderCostUsd
CalculatedCostUsd
PricingProfileHash
PricingVersion
RawUsageJson
DiagnosticsJson
```

Keep `AgentRunMetric` as a view/summary if needed, but do not make it the only source of truth.

## Required Reconciliation Outputs

After SB03, the following should be available through API and tests:

- execution-run usage observations
- process-run usage summary
- workflow-run usage summary
- end-to-end correlation usage summary when workflow/process correlation exists
- unknown-usage diagnostics
- provider/model pricing coverage diagnostics
- a diff report comparing old metric-derived cost vs new ledger-derived known cost on the Tetris run fixture

## Acceptance Tests

- Normal successful run records observed input/cached/output/total usage.
- Required finalizer short-circuit does not zero usage.
- Failed run after usage-bearing provider activity records usage.
- Structured-output repair records child usage.
- Background/continuation path records each usage-bearing response or proves cumulative aggregation.
- Usage-null response records an explicit unknown status.
- Process actual cost aggregates known usage observations and lists unknown observations.
- User-facing "tokens used" answer uses known ledger totals, not `EstimatedCost`.
