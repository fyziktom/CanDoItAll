# Cache And Source Of Truth

## Source Of Truth Rules

- Process definitions, run state, step state, outbox records, artifacts, decisions, escalations, approvals, and AgentFramework execution records remain authoritative in their existing stores and services.
- `IMemoryCache` may only hold derived observation projections. Cached entries must be safe to discard at any time.
- Every observation snapshot must include enough metadata to reason about freshness: `ObservedAtUtc`, `SourceMaxUpdatedAtUtc` when available, `Revision`, and `ProcessObservationStaleness`.
- If a source read fails, return a typed observation failure or stale snapshot with explicit failure metadata. Do not silently return stale data as if it were current.

## Dedicated Cache Wrapper

Create a dedicated singleton wrapper such as `ProcessObservationCache` instead of injecting the shared `IMemoryCache` throughout projection code. The wrapper owns:

- typed cache keys
- cache entry size assignment
- absolute and sliding expiration policy
- per-key async stampede protection
- invalidation by process definition, run, project, and agent execution identifiers
- metrics/logging for hits, misses, evictions, stale reads, and slow factories

This avoids the Microsoft Learn `IMemoryCache` size-limit trap where a shared size-limited cache can fail if unrelated entries do not specify sizes.

## Cache Key Shape

Keys must be typed and include all dimensions that affect authorization or data shape:

- project id
- process definition id when scoped
- process run id when scoped
- selected window/page/filter
- query mode, such as dashboard, run snapshot, stage snapshot, timeline page, dialog payload
- user/tenant/authorization scope if observation results can differ by caller

Do not use free-form strings for key construction outside the cache wrapper.

## Initial Expiration Policy

The exact values should be options-backed and verified under load. Use this as the implementation starting point:

| Projection | Suggested freshness | Invalidation |
| --- | --- | --- |
| active dashboard summaries | 1-3 seconds | run, step, outbox, escalation, AgentFramework change |
| inactive dashboard summaries | 10-30 seconds | definition, run, or project change |
| definition metadata | 2-5 minutes | save, publish, delete, import, catalog warmup |
| selected run details | 5-15 seconds | run, step, artifact, decision, escalation, approval, AgentFramework change |
| timeline pages | 5-30 seconds by activity | any event source update for the run/project |
| static option lists | 2-5 minutes | project or catalog invalidation |

Use both absolute and sliding expiration for hot projections so a constantly viewed dashboard still refreshes from source on a bounded cadence.

## Stampede Protection

For each typed cache key, allow one active factory to repopulate the value. Concurrent callers should await the same factory when safe. The factory must be cancellable and must not hold EF entities or scoped services beyond the read operation.

## Invalidation Points

Invalidation must happen after successful authoritative changes in these areas:

- definition save, publish, delete, import, and catalog synchronization
- run start, status transition, pause/stop/cancel/retry, and rerun
- step start, completion, failure, block, skip, and assignment update
- artifact, decision, work brief, conformance observation, and evidence changes
- outbox enqueue, dispatch, completion, retry, and dead-letter state changes
- escalation journal, operator approval, and operator control-plane changes
- AgentFramework execution run, approval, health, and attempt updates

If an implementation cannot hook a source immediately, record the explicit stale-window risk and compensate with short TTL until the invalidation hook is added.

## Scale-Out Limit

This plan is for local in-process projection caching. If Processes runs on multiple app instances, `IMemoryCache` does not invalidate other nodes. The architecture must leave room for a later distributed invalidation channel or distributed cache, but the first implementation should not introduce that complexity unless deployment topology requires it.
