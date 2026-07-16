# Project planning analytics and agent access

Status: Accepted for implementation, 2026-07-16

## Context

Project tasks now carry delivery schedule, expected effort, expected cost, progress, dependency, and resource-assignment data. Project structure and Gantt remain projections over the same authoritative records. Agents and future dashboards need useful plan summaries without loading every project-structure record or issuing a separate query per task.

The existing `ProjectStructureAgentService` already owns a broad command surface and `ProjectStructureAnalyticsService` reports operation/audit activity. Neither is the right owner for plan economics or schedule-state aggregation. A plan summary also must not become a cached second source of truth.

## Decision

Add a focused `ProjectPlanAnalyticsQueryService` and a deterministic `ProjectPlanSummaryCalculator`.

The query service has two input paths:

- The persisted path applies the project id, canonical task/workflow types, relevant link kinds, system-managed exclusion, and work-item-assignee role in database queries. It uses no-tracking projections plus a compact assignee-binding projection that excludes display names, notes, allocation, and other UI-only assignment fields.
- The loaded-surface path accepts an already available `ProjectStructureSurface` and compact assignee-binding collection. Gantt or other callers that already hold those records reuse them instead of going back to the database.

Both paths create the same compact snapshot and use the same calculator. Core indexing and aggregation are O(N+E) relative to selected tasks, links, and bindings; ordering each preview subset is O(N log N), while returned previews are bounded by the request and capped at 100. Aggregate-only callers can set the preview limit to zero. Each task preview carries the full blocker count and at most 20 deterministic blocker ids; blocker-id samples are materialized only for selected previews. No cross-request cache, summary table, or hidden fallback is introduced.

Relevant composite indexes start with `ProjectId` and then the discriminators used by the queries: object type/subtype/system ownership, parent/type/system ownership, link kind/system ownership, and assignment kind/node key. The same migration canonicalizes legacy case variants of the WorkItem `task` subtype, and all direct Workbench persistence paths preserve that lowercase invariant. Index deployment must use the normal database migration path.

The summary contains:

- state counts and ratios for unscheduled, planned, ready, running, waiting, blocked, completed, and cancelled tasks;
- earliest start, latest end, delivery lead time, and summed scheduled task duration;
- expected effort in hours and configured man-days;
- task-weighted and effort-weighted progress;
- expected-cost totals grouped by normalized currency;
- resource binding count/share and overlapping task coverage for person, agent, workflow, and process bindings;
- bounded running, blocked, and waiting task previews with full blocker counts and bounded blocker-id samples;
- explicit completeness counters and warnings for missing or invalid schedule, progress, estimate, resource, and dependency-cycle data.

## Economic and resource semantics

Cost values are expected task cost, not invoices, actual consumption, or a price reconstructed from incomplete resource profiles. A missing amount is unknown and is never converted to zero. Totals remain separated by currency; the service does not invent an exchange rate.

Resource binding share and resource task coverage answer different questions:

- binding share divides a group binding count by all resource bindings;
- task coverage divides tasks having at least one binding from a group by all tasks.

Coverage groups overlap. One task can be assigned to a person and agent and also carry workflow and process bindings, so coverage percentages need not total 100%. `ExclusiveTaskCount` identifies tasks covered by only that group. Cost is not attributed across resource groups because current task/resource data does not define a safe allocation rule.

Completeness is part of the result contract. Missing and invalid schedule counts are disjoint, as are missing/untracked and invalid progress counts. Consumers must show missing schedule, effort, expected-cost, progress, and assignment plus invalid schedule, progress, metadata, mixed-resource, and dependency-cycle-affected counts when they materially affect a conclusion. The dependency count includes tasks that cannot be topologically resolved after cycle detection; it must not be presented as the number of nodes directly inside a cycle or as proof that a completed historical cycle currently blocks downstream work.

## Agent authorization boundary

Basic project-structure read access continues to expose ordinary task facts through standard read tools. Deeper plan aggregation uses `project_plan_summary_get` and requires all of the following at runtime:

- the agent is active, non-template, and allowed to use tools;
- project-structure read access covers the requested project;
- the assigned capability catalog contains the exact `project-plan-summary-get` tool mapping;
- the assigned capability catalog contains the exact `project-plan-analysis-inline-skill` mapping.

The provider reloads agent settings and capability assignments before the invocation so a stale chat runtime cannot preserve revoked access.

Task mutation and non-task structure mutation are separate authorities. `CanWriteTasks` allows task-specific create/update operations for covered projects without granting generic project-structure mutation. `CanWriteNonTaskStructure` permits guarded generic mutations while rejecting task creation, direct task mutation or reclassification, task links, and subtree operations that would affect a task; adding a non-task child beneath a task remains allowed. Existing broad `CanWrite` remains the unrestricted superset for backward compatibility. Analysis capability alone grants no mutation permission, and neither narrow authority implies the other.

The HTTP project-structure API is a separate control-plane boundary. Its bearer/API authorization does not establish an internal `AgentDefinition`, so it does not pretend to enforce agent capability assignments or `CanWriteTasks`. Clients acting on behalf of an agent must use the governed runtime tool path when agent-level policy is required.

## Responsibility and dependency map

| Responsibility | Owner |
| --- | --- |
| Canonical task, link, estimate, and schedule records | Existing project-structure persistence and mutation services |
| Compact person and agent task-binding projection | `IProjectPartyIntegrationBridge` |
| Database-filtered plan read | `ProjectPlanAnalyticsQueryService` |
| In-memory deterministic aggregation | `ProjectPlanSummaryCalculator` |
| Runtime project/capability checks | `ProjectStructureAgentAuthorizationService` |
| Runtime tool attachment and receipts | Existing Workbench runtime provider and AgentFramework tool policy |
| HTTP integration authorization | Existing authenticated project-structure API boundary |
| Interpretation guidance | `project-plan-analysis-inline-skill` |

```text
Gantt / loaded project surface -----> ProjectPlanSummaryCalculator
Authenticated HTTP API ------------> ProjectPlanAnalyticsQueryService
Agent runtime provider ------------> ProjectPlanAnalyticsQueryService
                                          |
                                          +--> filtered EF projections
                                          +--> assignment bridge
```

The calculator is a sealed, side-effect-free class rather than an interface with one trivial implementation. The query service is the application boundary; EF and CRM/HR assignment details do not leak into the summary contract.

## Rejected alternatives

- Extending `ProjectStructureAgentService`: it would add another responsibility to an already broad service and make independent aggregate testing harder.
- Reusing `ProjectStructureAnalyticsService`: operation/audit analytics and plan/economic projections have different source data and semantics.
- Fetching a complete `ProjectStructureSurface` for every summary: dashboards usually need a narrow projection, not every node, metadata field, visual state, and link.
- Querying per task or per resource: creates N+1 database work and scales poorly across projects.
- A summary cache or denormalized read table now: without invalidation owned by every task, link, assignment, and estimate mutation it becomes a second source of truth. A read model can be added later only with measured demand and explicit transactional/event consistency.
- Combining currencies or treating unknown cost as zero: both create false financial precision.
- Making resource groups mutually exclusive: it loses valid mixed assignments and misstates utilization.
- Authorizing by tool name, agent label, workload, or skill alone: editable metadata is not an authority boundary.
- Applying internal-agent settings to the bearer HTTP route: the route has no trusted internal-agent identity to evaluate.

## Validation and evolution

Unit tests cover dependency direction, completed-cycle behavior, state precedence, untracked versus invalid progress, missing versus invalid schedules, mixed currencies, missing estimates, overlapping resources, project-isolated bindings, deduplication, preview bounds, exact capability gates, and task-only access behavior. Provider-backed endpoint smoke validation proves the persisted query can execute against PostgreSQL. Full loaded-surface mapping parity and SQL-plan regression tests remain explicit follow-up coverage before a multi-project dashboard ships.

Performance validation scans the projection and calculator for accidental N+1 queries, repeated full scans, per-item database calls, avoidable intermediate materialization, synchronous blocking, and unbounded outputs. A portfolio dashboard must not call the per-project endpoint in an unbounded loop. When cross-project demand is concrete, add a bounded bulk query or separately versioned read model with explicit refresh lineage and freshness metadata rather than hiding cache behavior in this service.
