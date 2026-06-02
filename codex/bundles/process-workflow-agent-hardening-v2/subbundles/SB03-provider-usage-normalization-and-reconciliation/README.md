# SB03 Provider usage normalization and billing reconciliation

## Status

Ready for implementation.  
Critical foundation: **Yes**

## Objective

Make provider usage observations complete enough to explain OpenAI billing differences and to serve as the source of truth for execution, process, and workflow cost.

## Covered Inputs

R05, R06, R07; source evidence E06, E07, E08, E12, E15.

## Prerequisites

Read `MafAgentRuntime`, `AgentFrameworkWorkspaceExecutionService.Usage`, `ProviderPricingModels`, process costing, workflow LLM invoker, and OpenAI Responses usage docs.

## Exact Source References

- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.Usage.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/Workflows/MafWorkflowLlmComponentInvoker.cs`

## Deliverables

- `IProviderUsageNormalizer` with provider-specific implementation for OpenAI/AzureOpenAI raw usage details.
- Normalized cached input, reasoning, provider total, provider response id, request id, status, and raw JSON.
- Finalizer short-circuit/recovery tests proving observations are preserved and aggregate metrics are derived from observations or marked legacy.
- OpenAI billing/export reconciliation report format: internal response IDs, source phases, token totals, known/unknown deltas.
- UI/API fields distinguishing known, estimated, unknown, missing, and usage-null observations.

## Dependency Impact

This subbundle affects downstream proof and must be treated as a dependency exactly as modeled in `bundle://plan/01-phase-plan.md`. If this subbundle fails, all downstream subbundles that depend on its runtime behavior or proof contract must be reopened.

## Validation Depth

Critical subbundle validation requires semantic adequacy proof: shallow-pass trap, adversarial negative proof, semantic positive proof, anti-stub audit, raw-note literal closure, changed-file hashes, and command/browser transcripts where applicable.

## Implementation Steps

1. Create raw usage fixtures from OpenAI-style Responses payloads with cached tokens, reasoning tokens, total tokens, and usage-null responses.
2. Add provider usage normalizer and route all MAF usage observations through it.
3. Update cost summaries to use provider total tokens for reconciliation while avoiding double-charging reasoning tokens when they are included in output tokens.
4. Make `AgentRunMetric` derived from known observations or explicitly legacy/fallback; avoid independent totals in finalizer paths.
5. Add reconciliation command/report that can compare internal observations with OpenAI billing/export data for a time window or response id list.
6. Run a small live provider smoke test when credentials are available; otherwise mark reconciliation blocked rather than closed.

## Scope Exceptions

None planned. If implementation discovers a legacy compatibility exception, record it in this file and in `traceability/` before continuing.

## Do Not Do

Do not invent costs for `UsageUnavailable` or `MissingAfterProviderActivity`. Do not hide unknown usage as zero actual cost. Do not require provider billing secrets in committed proof.

## Acceptance Checklist

- [ ] Source references were reopened before editing.
- [ ] Implementation is the smallest correct change set for this subbundle.
- [ ] Failing-first proof was captured for behavior-changing critical work.
- [ ] Passing proof was captured after implementation.
- [ ] Anti-stub audit was run.
- [ ] Raw notes owned by this subbundle were closed or explicitly blocked.
- [ ] Downstream dependency impact was reviewed before moving on.

## Proof Required

Raw usage fixture tests, finalizer usage tests, provider-failure usage tests, workflow LLM usage tests, process cost aggregation tests, and one live or imported OpenAI reconciliation report with redacted identifiers.

## Browser Validation Logging

SB08 should show UI state for known/unknown usage; core SB03 browser proof is N/A.

## Progression Gate

SB04 must record non-empty usage observations for real provider runs, or explicitly mark provider E2E blocked.

## Suggested Agent Prompt

You are implementing `SB03 Provider usage normalization and billing reconciliation` in `fyziktom/CanDoItAll` on branch `development`. Read this subbundle README, the root README, `plan/01-phase-plan.md`, `traceability/`, and all exact source references before editing. Implement only this subbundle. Do not close it without the required semantic proof, transcripts, changed-file hashes, anti-stub audit, and raw-note closure update.
