# C# Architecture Gate

## Status

- Preparation gate: `Pass`
- Implementation evidence gate: `Pass — AC01, AC02, and AC03 approved`
- Closure decision: `Approved — completed-stage validator passed`
- CodeAnalytics evidence: `Unavailable`; manual no-reference/dependency/source inspection and the Release solution build are the explicit fallback.

## Preparation Findings

| Gate | Result | Evidence |
| --- | --- | --- |
| Responsibility ownership | Pass | `bundle://architecture/00-csharp-current-state-inventory.md` and boundary map identify each owner. |
| Dependency direction | Pass as planned | No new reference is required; forbidden edges are explicit in `bundle://architecture/02-csharp-dependency-direction.md`. |
| Pattern justification | Pass | Four pattern records include rejected alternatives and unit seams. |
| Testability | Pass as planned | Direct positive/negative/composition proof is specified in `bundle://architecture/04-csharp-testability-plan.md`. |
| Partial policy | Pass as planned | New partial/nested types are forbidden; existing AgentFramework partial cluster is not extended. |
| CodeAnalytics | Gap accepted for preparation | Tool unavailable by explicit input; SB03 retry/manual proof required. |

## Execution Gate Table

| Check | Required execution result | Status |
| --- | --- | --- |
| Before/after references/cycles | No new project/package edge; CodeAnalytics or manual graph evidence | `Pass — no changed csproj/props/targets/solution file; existing dependency direction is unchanged` |
| Old-owner shrink/non-growth | Home thin; ProjectsService, aggregate overview, process query, Agent facade do not gain dashboard policy | `Pass — Home is snapshot-only; narrow query owners contain the new policy` |
| Direct unit seams | All four queries/cache directly instantiated and behavior-tested | `Pass for AC01 — query 28/28; cache/loader/runner 18/18; workflow rerun 13/13` |
| Negative separation proof | Broad methods/stores/enrichers provably not invoked | `Pass for AC01 — adversarial canonical-state, bounds, source-failure, concurrency, cancellation, and scope-lifetime cases` |
| Composition smoke | Singleton service/cache/runner plus fresh scoped loader graph and app build/run succeed | `Pass for AC01 — Web and AgentFramework Release builds 0 errors; runner composition included in 18/18` |
| Browser agreement | UI matches boundary/composition and no hidden load/error policy in Home | `Pass — six inspected screenshots cover populated, alternate tab, empty, stale error, target viewport, and supplementary small viewport` |

## Closure Rejection Rules

Reject and reopen the owner if a partial file, project reference, service location outside `DashboardSnapshotLoadRunner`, retained/reused scoped dependency, direct loader store, broad facade call, mutable/unbounded cached collection, user-specific shared-cache value, untestable wrapper, silent fallback, or fake interface separation appears. Do not close on file existence, row counts alone, or screenshots without source/negative proof.

## AC01 Decision

- Decision: `Approved` on 2026-07-22.
- Scope: SB01 foundation only. The approved evidence covers typed bounded queries, canonical active/fallback policy, immutable loader graph, cache/coalescing/failure/cancellation/runtime-key behavior, and fresh scoped-loader lifetime.
- Progression: SB02 is unlocked. AC02, AC03, final browser/performance proof, and bundle closure are not implied.

## AC02 Decision

- Decision: `Approved` on 2026-07-22 after the first review's exact-label and wall-clock timer-test findings were corrected.
- Scope: AppComponents-owned semantic `QuickActionCard`, snapshot-only Home rendering, exact routes/labels/states, deterministic five-minute timer/force/disposal behavior, and no page-local primitive duplicate.
- Evidence: `bundle://evidence/SB02/component-behavior-and-ac02.md` and Home/QuickActionCard tests 7/7.

## AC03 Decision

- Decision: `Approved` on architecture, dependency direction, performance, build, tests, migration consistency, browser behavior, and completed-stage validation.
- No `.csproj`, `.props`, `.targets`, `.sln`, or `.slnx` file changed. No new dashboard partial/nested type or service locator exists; the generated EF migration partial pair is normal framework output.
- The Release solution build passed with 0 errors. Fifty-four focused tests passed: queries 28, cache/loader/runner 18, Home/QuickActionCard 7, and isolated real-app Playwright 1.
- The two-pass performance audit found no unresolved critical/moderate issue. Cache coalescing/cadence, hard-five query bounds, fresh scope lifetime, cancellation, locks, allocations, and PostgreSQL plans are recorded in `bundle://evidence/SB03/performance-architecture-browser-closure.md`.
- CodeAnalytics remains unavailable. This is a transparent tooling gap, not a false clean claim; the manual dependency proof and successful whole-solution build are accepted fallback evidence.
- Completed-stage bundle validation passed after the exact subbundle statuses, gate table, portable proof references, and raw-note citations were reconciled.
- Independent AC03 rereview returned `Approved` with no remaining blocker after the isolated empty/stale-error browser proof and completed-stage validator pass.
