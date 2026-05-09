# 03-projection-cache-and-invalidation

## Status

- `Completed`

## Objective

- Add a bounded, explicit, read-only projection cache and invalidation path for process observation snapshots without creating a second source of truth.

## Success Criteria

- Observation cache is wrapped behind a dedicated service instead of scattered direct `IMemoryCache` usage.
- Cache keys are strongly typed and include project, query, and authorization-relevant dimensions.
- Entries have size, TTL, staleness metadata, and explicit failure behavior.
- Invalidation hooks are wired after successful authoritative process changes where feasible.
- Tests prove cache hit/miss, invalidation, stale/failure behavior, and source-of-truth preservation.

## Covered Inputs

- R-004, R-005, R-006, R-010, R-012.
- Microsoft Learn `IMemoryCache` guidance in `inputs/01-source-artifacts.md`.
- Cache policy in `architecture/02-cache-and-source-of-truth.md`.
- Observation contracts from subbundle `02`.

## Prerequisites

- `01-current-state-observation-map` is complete.
- `02-observation-contracts-and-boundary` is complete and its progression gate is passed.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessesService.RuntimeReadQuery.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessRuntimeStateOverviewService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOutbox.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Runtime\ProcessOperatorControlPlane.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Services\ProcessesModuleServiceCollectionExtensions.cs`

## Deliverables

- `ProcessObservationCache` or equivalent dedicated wrapper around `IMemoryCache`.
- Options model for cache size, TTLs, active/inactive freshness, and slow-query thresholds.
- Typed cache-key factories and invalidation indexes.
- Per-key async stampede protection.
- Cache metrics/logging for hit, miss, stale, eviction, invalidation, and source-read failure.
- Invalidation calls after successful relevant process writes.
- Tests for all critical cache behavior.
- New observation cache files under the observation boundary selected in subbundle `02`.

## Dependency Impact

- `04-ui-observation-shell-and-dialogs` depends on bounded, truthful snapshots so live UI refresh does not overload process core.
- `05-ai-driven-dashboard-intent-bridge` depends on cache keys that correctly include filters and caller scope.
- `06-validation-performance-and-rollout` depends on this phase for measurable performance improvement.
- Weak cache proof risks stale or incorrect process state in every future dashboard view.

## Validation Depth

- `Process-critical closure`

## Implementation Steps

1. Confirm the observation contracts and key dimensions from subbundle `02`.
2. Add options-backed cache policy with conservative defaults.
3. Implement the dedicated cache wrapper, typed keys, size assignment, TTLs, and per-key async repopulation guard.
4. Ensure cached values are immutable projection DTOs only. Do not cache EF entities, scoped services, or mutable component state.
5. Wire observation service reads through the cache for selected read shapes, starting with dashboard summaries and selected-run snapshots.
6. Add invalidation hooks after successful authoritative changes. If a source cannot be hooked safely in this phase, record a bounded TTL mitigation and follow-up.
7. Add unit/integration tests for hit/miss, invalidation, cancellation, stale result exposure, source-read failure, and no-cache-after-failed-write behavior.
8. Capture performance counters or logs for representative active-run reads.
9. Update the execution report with cache policy, unresolved invalidation sources, and proof.

## Scope Exceptions

- Distributed cache or cross-node invalidation is out of scope unless deployment topology requires it before rollout.
- UI migration remains in subbundle `04`.
- SignalR remains out of scope unless measurements show polling cannot meet the target.

## Do Not Do

- Do not treat cache as authoritative.
- Do not return stale snapshots without explicit staleness/error metadata.
- Do not use a size-limited shared `IMemoryCache` directly from unrelated services.
- Do not cache user-specific data without including user/authorization scope in the key.
- Do not introduce broad `forceRefresh` calls from UI that bypass all coalescing.

## Acceptance Checklist

- Cache wrapper is the only new code that directly manages process observation cache entries.
- Every cache entry has size and expiration policy.
- Cache keys are typed and include project and query dimensions.
- Invalidation occurs only after successful authoritative writes.
- Tests prove failed source reads are visible.
- Tests prove invalidated entries are not reused.
- Logs include actionable state without sensitive data.

## Proof Required

- `dotnet build CanDoItAll.slnx`
- `dotnet test tests\CanDoItAll.Tests.Integration\CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProcessRuntimeReadQueryServiceTests|FullyQualifiedName~ProcessActiveRunSummaryPerformanceTests|FullyQualifiedName~ProcessRuntimeOperatorReadModelTests"`
- New observation cache test command.
- Execution report section listing invalidation hooks and any temporary TTL-only gaps.

## Browser Validation Logging

- N/A unless this phase touches visible UI. If it does, run the `04` browser proof before closing.

## Progression Gate

- Downstream subbundles may continue only when cache tests prove invalidation and stale/error semantics, and the execution report explicitly confirms no cached projection is treated as source of truth.

## Suggested Agent Prompt

```text
Implement this subbundle only.
Work outcome-first: preserve the listed scope boundaries, verify prerequisites before editing, make the smallest correct change set, capture the required proof, update the execution report rows, and stop if the progression gate cannot honestly pass.
```
