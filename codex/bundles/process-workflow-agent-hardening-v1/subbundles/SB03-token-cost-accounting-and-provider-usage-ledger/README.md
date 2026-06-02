# SB03 - Token Cost Accounting And Provider Usage Ledger

## Status

Ready for implementation. Classification: **Critical foundation**.

## Objective

Fix token/cost accounting so process and workflow cost reporting reflects all known provider usage and explicitly represents unknown usage. Replace metric-only aggregation with a durable provider usage ledger or equivalent observation model.

## Covered Inputs

Covers user-reported OpenAI billing mismatch, process actual cost undercount, finalizer short-circuit zero tokens, failed-run estimated tokens, structured-output repair usage, background/continuation usage, workflow summarization usage, and known/unknown cost display.

## Prerequisites

SB01 completed. SB02 may run in parallel only if code ownership conflicts are coordinated; SB03 must not rely on unmerged dispatch refactors.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Automation/Dispatch/ProcessRunAutomationDispatchService.Costing.cs`
- `repo://src/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/AgentFrameworkWorkspaceExecutionService.ExecutionRuns.cs`
- `repo://src/CanDoItAll.AgentFramework.Maf/Runtime/MafAgentRuntime.cs`
- `repo://src/CanDoItAll.AgentFramework.Core/Execution/*OutputRepair*`
- `repo://tests/CanDoItAll.Tests.Unit/ProviderPricingTests.cs`
- `repo://codex/bundles/chatgpt-pro-process-workflow-agent-hardening-inputs-v1/inputs/api-captures/agent-execution-runs-for-process-6724.json`
- `bundle://analysis/03-token-cost-accounting-audit.md`
- `bundle://requirements/03-token-accounting-requirements.md`

## Deliverables

- Provider usage observation model/table/store.
- Usage recorder service.
- Pricing aggregation service.
- Process run usage summary API/DTO.
- Workflow run usage summary API/DTO where model calls exist.
- Migration/backfill from legacy `AgentRunMetric`.
- Tests for normal, finalizer, failure, repair, background, usage-null, and legacy paths.
- Old-vs-new Tetris cost reconciliation artifact.
- Proof manifest and semantic invariants for SB03.

## Dependency Impact

SB07 UI and SB08 E2E depend on honest usage reporting. SB09 will red-team token undercount.

## Validation Depth

Deep semantic validation. Must include failing-first proof for finalizer short-circuit zero-token undercount and failure-after-provider-call undercount.

## Implementation Steps

1. Inspect every provider invocation path, including runtime, finalizer, repair, workflow summarization, image generation if relevant, and manager chat.
2. Define usage observation schema and persistence.
3. Capture usage as close to provider response as possible.
4. Preserve usage on success, failure, cancellation, background, continuation, repair, and finalizer paths.
5. Represent usage-null states explicitly.
6. Update process cost sync to aggregate ledger rows.
7. Keep legacy metric summary compatible.
8. Add API/DTO fields for known/estimated/unknown usage.
9. Backfill legacy metrics with migration tests.
10. Produce reconciliation report for existing Tetris evidence.

## Scope Exceptions

If OpenAI billing export/API is unavailable locally, do not block. Instead, reconcile internal ledger against captured provider response usage and mark provider-dashboard reconciliation as pending external verification.

## Do Not Do

- Do not calculate actual tokens from `EstimatedCost`.
- Do not set finalizer/failure tokens to zero when provider usage exists.
- Do not silently drop unknown usage.
- Do not double-count retries or background polls.
- Do not include upstream workflow usage in process run cost unless correlation is explicit.

## Acceptance Checklist

- [ ] Provider usage observation schema exists.
- [ ] Normal run usage is recorded.
- [ ] Finalizer short-circuit usage is recorded or marked unknown with provider-activity diagnostic.
- [ ] Failed-after-provider-call usage is recorded.
- [ ] Structured-output repair usage is linked.
- [ ] Background/continuation usage is cumulative or separately itemized.
- [ ] Process actual cost aggregates usage ledger.
- [ ] UI/API can report known/unknown token totals.
- [ ] SB03 proof manifest exists.

## Proof Required


Because this is a critical subbundle, the Semantic Adequacy Gate proof must include:

- `proof/SBxx/manifest.md`
- `proof/SBxx/semantic-invariants.md` or `.json`
- changed-file hashes
- command transcript paths
- source assertions
- shallow-pass trap
- adversarial negative proof
- semantic positive proof
- anti-stub audit
- raw-note literal closure
- dependency smoke proof where stated

Production Behavior Artifact Matrix required for provider usage observations, usage summary records, pricing profile records, and process actual cost synchronization. Include producer, consumer, lifecycle, and negative-test citations.


## Browser Validation Logging

N/A unless process/agent cost UI is changed in this subbundle. If UI is touched, log process detail route, viewport, screenshots, known/unknown usage display, and console evidence.

## Progression Gate

SB03 passes only when the old metric-derived undercount class is demonstrably closed by usage-ledger tests and reconciliation output.

## Suggested Agent Prompt

Implement SB03 only. Build durable provider usage accounting and prove finalizer/failure/repair/background paths cannot silently undercount.
