# 01-token-usage-cost-accounting

## Status

- `Completed`

## Objective

- Correct the provider usage accounting pipeline so successful agent runs persist provider-reported input, cached input, and output tokens, and provider pricing calculates costs from those values without prompt double counting.

## Success Criteria

- `AgentRuntimeResponse` carries cached input tokens.
- Microsoft Agent Framework usage mapping includes `CachedInputTokenCount`.
- Auto-approved continuation aggregation includes cached input tokens.
- Successful run metrics persist provider input/output/cached input tokens without adding local prompt estimates.
- Pricing tests or execution-run tests prove cached input contributes to known cost and non-cached providers remain zero cached tokens.

## Covered Inputs

- N001 / R001 / R003: improve usage, price, and statistic calculations.
- N002 / R001: fix local accounting mismatch drivers.
- N003 / R002 / R003: count output and cached tokens for OpenAI.
- N004 / R002 / R003: preserve zero cached-token behavior for providers without cached usage.

## Prerequisites

- Prepared bundle exists at `codex/bundles/process-token-cost-graphs-v1`.
- Current-state analysis has been reviewed and still matches inspected code.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Core/Contracts/Contracts.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Chat.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Models/Conversations/ConversationModels.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/ProviderPricingTests.cs`
- `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Integration/AgentFrameworkExecutionRunTrackingIntegrationTests.cs`

## Deliverables

- Runtime response exposes cached input tokens.
- MAF response conversion maps cached input tokens safely.
- Run continuation aggregation returns total cached input tokens.
- `AgentRunMetric.CachedInputTokens` is set for successful runs.
- Successful metric input token count is no longer inflated by local prompt estimates.
- Tests prove cached-token and no-double-count behavior.

## Dependency Impact

- SB02 and SB03 depend on this phase because all historical cost graphs and process/run graph views are built from persisted execution metrics.
- Weak proof here invalidates any later cost graph proof because the graphs could render accurate-looking but wrong data.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Add cached input token exposure to `AgentRuntimeResponse` with the smallest compatible contract change.
2. Map `UsageDetails.CachedInputTokenCount` in `MafAgentRuntime`.
3. Add cached input aggregation to `ContinueAutoApprovedRunAsync` and its callers.
4. Persist cached input tokens in successful continuation and normal execution metrics.
5. Remove successful-run prompt estimate addition from provider-reported input token metrics.
6. Add or update focused tests for cached token propagation, known cost calculation, and no prompt double counting.
7. Capture command proof and update `reviews/01-execution-report.md`.

## Scope Exceptions

- No external billing API reconciliation.
- No provider price table expansion unless existing tests expose a missing known price that blocks cached-token calculation.
- Failure metrics may continue using local estimates when no provider usage exists.

## Do Not Do

- Do not add fallback prices for unknown provider/model pairs.
- Do not infer cached tokens when a provider does not report them.
- Do not refactor unrelated execution-run tracking or process dispatch code.

## Acceptance Checklist

- Cached input token value from a provider response can be observed on the persisted metric.
- Output token accounting remains intact.
- Non-cached providers persist zero cached input tokens.
- Known pricing includes cached input token cost when the metric has cached tokens.
- Successful run input tokens equal provider-reported input tokens, not provider input plus prompt estimate.

## Proof Required

- `proof/SB01/manifest.md` summarizing changed accounting invariants.
- Transcript for targeted tests under `proof/SB01/transcripts/`.
- Relevant test command output for unit/integration tests.
- Updated execution-report row for SB01.

## Browser Validation Logging

- `N/A`: this subbundle changes backend accounting and persistence behavior, not browser-visible layout.

## Progression Gate

- SB02 must not start until targeted tests pass and `proof/SB01/manifest.md` states the cached-token, output-token, provider-input, and pricing invariants.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
