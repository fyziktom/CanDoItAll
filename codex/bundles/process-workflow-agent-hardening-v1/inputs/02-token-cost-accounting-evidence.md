# Token And Cost Accounting Evidence

## Observed User Concern

The user sees more OpenAI API token usage in billing than the process run reports. This is plausible from the current code shape.

## Current CanDoItAll Cost Sync Path

`repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs` currently:

1. Lists providers.
2. Lists agent execution runs filtered by `ProcessRunId`.
3. Loads each execution run detail.
4. Iterates `detail.Metrics`.
5. Deduplicates metrics by metric id.
6. Resolves metric cost from either `metric.CostUsd` or provider pricing.
7. Sums the known metric costs.
8. Writes `ProcessRun.ActualCost`.

This means the process cost is only as complete as the persisted `AgentRunMetric` rows linked to that `ProcessRunId`.

## Current Pricing Calculator Behavior

`repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs` supports:

- input tokens
- cached input tokens
- output tokens
- provider model prices
- fallback from `metric.CostUsd` to calculated cost

It currently does not model these as separate first-class billable observations:

- provider response id
- request/response transport id
- background poll response id
- reasoning tokens
- total tokens returned by provider
- usage-null status
- failed-after-provider-call usage
- finalizer short-circuit usage
- output repair model usage
- workflow summarization model usage
- provider dashboard reconciliation status
- estimated-vs-observed usage coverage

## Current MAF Runtime Usage Capture Risk

`repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs` normally returns usage from `response.Usage` after streaming updates are aggregated. However, required-finalizer short-circuit paths build an `AgentRuntimeResponse` with `InputTokens: 0` and `OutputTokens: 0`. Process-step runs are exactly the place where required governed output/finalizer behavior matters, so this is a high-priority undercount risk.

## Failure Path Risk

`repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs` creates failure metrics with estimated prompt tokens or zero tokens, even when the provider may have already streamed output/tool calls before a failure. The observed failed QA run caused repeated tool invocation failure. If provider usage occurred before the guard threw, the persisted metric can undercount.

## Structured Output Repair Risk

The execution service calls an output repair service when structured output validation fails. The current fetched path did not show repair usage being joined into the parent run metric. Codex must inspect the concrete repair service implementation and verify whether repair model calls produce metrics linked to the same process run, execution run, or a child usage ledger entry.

## Background And Cancelled Response Risk

Responses-style APIs can return usage on completed responses and may return `usage: null` on some cancelled/background states. The ledger must record both the positive usage and the unknown-usage case so the UI can say "known cost", "estimated cost", and "unreconciled provider calls" instead of silently showing a precise-looking number.

## Required Target

The refactor must introduce a provider usage ledger or equivalent durable usage observation model. Process actual cost must aggregate from that ledger, not only from one metric row per agent execution run.

The ledger must support:

- one row per provider billable response or usage-bearing event
- parent process run id
- parent process step id
- parent agent execution run id
- parent workflow run id when applicable
- provider name/kind
- model
- provider response id
- input tokens
- cached input tokens
- output tokens
- reasoning tokens when available
- total tokens when available
- tool call count
- provider reported cost when available
- calculated cost
- pricing version
- usage status: observed, missing, estimated, provider-error-no-usage, cancelled-no-usage, unsupported-provider, ignored-private-provider
- source phase: normal-run, approval-continuation, background-poll, finalizer-short-circuit, finalizer-recovery, structured-output-repair, workflow-executor-summarization, manager-chat, manual-chat
- reconciliation status against provider billing export or API usage endpoint where available

## Acceptance Constraint

After SB03, the process UI/API must not answer "how many tokens did it use?" with only the process estimated-cost field. It must expose at least:

- known input/cached/output/reasoning/total tokens
- calculated known cost
- estimated unknown/unobserved usage count
- list of execution runs or workflow nodes that contributed usage
- list of usage-bearing events with missing provider usage
