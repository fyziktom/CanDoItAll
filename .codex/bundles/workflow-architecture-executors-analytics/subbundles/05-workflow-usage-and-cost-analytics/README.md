# Workflow Usage And Cost Analytics

## Status

- `Completed`

## Objective

- Persist canonical correlated workflow usage observations and expose deterministic run/aggregate duration, token, model, and cost analytics.

## Success Criteria

- Provider observations retain identity, run/version/node/executor, provider/model, all token dimensions, pricing status/provenance, cost, timestamps, and origin correlation.
- LLM and usage-aware executor/plugin nodes flow observations through compiler/backend/persistence/query without loss.
- Analytics reports known cost separately from unknown observations and does not double count replay/retry/process rollups.
- API queries complete persisted data rather than current UI pages or event JSON.

## Covered Inputs

- WF-AN-01, WF-AN-02, workflow telemetry note, and “similar to processes” requirement.
- Missing executor usage propagation and lossy `WorkflowUsageMetrics` findings.

## Prerequisites

- SB01 contracts, SB03 usage-aware executors, and SB04 lifecycle/correlation gates pass.
- Process telemetry reader/projection patterns are inspected for reuse without coupling workflow UI to process types.

## Exact Source References

- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Providers/ProviderPricingModels.cs`
- `repo://src/MAF/Common/CanDoItAll.AgentFramework.Models/Workflows/WorkflowModels.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowLlmComponentInvoker.cs`
- `repo://src/MAF/Workflows/CanDoItAll.AgentFramework.Workflows.MafAdapter/MafWorkflowCompiler.cs`
- `repo://src/Modules/CanDoItAll.Modules.AgentFramework/Persistence/PersistentWorkflowStores.cs`
- `repo://src/Modules/CanDoItAll.Modules.Processes/Services/RuntimeIntegration/AgentFrameworkProcessRuntimeUsageTelemetryReader.cs`
- `repo://src/App/CanDoItAll.Web/Api/WorkflowsApi.cs`
- `repo://tests/Unit/CanDoItAll.Tests.Unit/WorkflowExecutorTests.cs`

## Deliverables

- Canonical workflow usage observation contract and storage/query boundary.
- Persistence entity/index/migration and producer integration for LLM/executor nodes.
- Workflow analytics query service with per-run, provider/model, state/backend, duration, token, cost, known/unknown summaries.
- API DTO/endpoint updates using query service and complete filters.
- Compatibility event summary generation where existing clients require it.

## Dependency Impact

- SB06 analytics panel consumes only the typed query service/DTOs.
- SB07 validates producer-to-consumer persistence and process rollup without double counting.

## Validation Depth

- `Critical data foundation` with migration, producer-to-consumer, arithmetic, replay, unknown pricing, failure, and multiple-model proof.

## C# Architecture Impact

- Completes the missing telemetry consumer side and separates immutable facts from projections/presentation.

## Boundary Ownership

- Provider/runtime producers create observations; persistence stores facts; analytics query projects; API/UI consume projections.

## Dependency Direction

- Projection depends on contracts/store and pricing abstractions, not Blazor or process UI. Process may consume workflow summaries through a neutral contract.

## Pattern Decision

- Use PSR-04 Append Facts, Project Analytics. Do not parse event JSON in API/UI or mutate opaque counters.

## Testability Contract

- Fixed observations/pricing/TimeProvider prove exact totals and duration.
- Stable observation IDs prove replay deduplication; unknown pricing/failure observations remain visible.

## Partial Class Policy

- Analytics producer/store/query are cohesive non-partial types. Do not add analytics branches to the 1701-line page code-behind.

## Architecture Proof Required

- Production behavior artifact matrix naming observation producer, persistence consumer, projection lifecycle, and negative/deduplication cases.

## Implementation Steps

1. Add failing tests for executor usage loss, missing detailed observations, paged totals, and unknown cost.
2. Define canonical observation/correlation/query contracts.
3. Persist observations with indexes/migration and integrate LLM/executor producers.
4. Build deterministic analytics query/projection and duration calculation.
5. Update API analytics DTO/endpoints and compatibility summaries.
6. Test multi-model, cached/reasoning/total tokens, failure, unknown pricing, replay, and process rollup.

## Scope Exceptions

- Historical runs without canonical observations report telemetry unavailable/partial; do not infer detailed tokens from incomplete summaries as authoritative data.

## Do Not Do

- Do not equate unknown cost with zero.
- Do not aggregate only the first eight/page-sized runs.
- Do not double count workflow child usage in root process analytics.

## Acceptance Checklist

- Canonical rows persist and query by run/node/provider/model.
- Executor and LLM usage both appear.
- Duration has explicit timestamp source and failure/cancel semantics.
- API totals are unpaged and expose completeness.
- Migration/integration/unit/build tests pass.

## Proof Required

- Failing-first usage-loss/paged-total transcript.
- Passing persistence/query/API arithmetic transcript with named observations.
- Adversarial replay, unknown pricing, multiple-model, failure, and double-count proof.
- `bundle://proof/SB05/manifest.md` and `bundle://proof/SB05/semantic-invariants.md` during execution.

## Browser Validation Logging

- `N/A in SB05: typed API/projection proof only; presentation is SB06.`

## Progression Gate

- Passed. Canonical producer-to-consumer, migration, dedupe, unknown usage, complete API totals, process rollup, and build proof are recorded in `bundle://proof/SB05/manifest.md`.

## Closure Evidence

- 22 focused unit/process-rollup tests, 2 API-handler validation cases, and 1 real PostgreSQL persistence test pass.
- Scoped production/test/migration builds pass with zero warnings and zero errors.
- The idempotent migration script includes exact terminal backfill, non-null usage RunId, and typed process-origin indexes; EF reports no pending model changes.
- Final scoped CodeAnalytics snapshot `snap-20260712211431-6ec6c129` has no blocking errors or project dependency cycles.
- Semantic invariants: `bundle://proof/SB05/semantic-invariants.md`.

## Suggested Agent Prompt

```text
Implement SB05 only. Persist immutable correlated provider observations, project deterministic complete analytics, preserve unknown usage and provenance, and prove LLM plus executor telemetry end to end without parsing event JSON in consumers.
```
