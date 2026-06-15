# SB26 Analytics, Graphs, Live Processes Dashboard, Snapshot Cache, And Time Windows

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild workspace graphs/analytics and the Live Processes dashboard over snapshot projections, including counters, history window filters, process filters, refresh behavior, activity, agents, graph tabs, tool analytics, cost/time summaries, and live incident actions.

## Covered Inputs

- REQ-026 to REQ-030, REQ-051, REQ-052.
- US-044 through US-048.
- AC-018 to AC-021, AC-035, AC-039, AC-040.

## Prerequisites

- SB25 evidence/coordination projections complete.
- SB10 monitoring projection workers and live/history snapshot cache complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessObservationGraphsPanel.razor`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessObservationCacheTests.cs`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/live-processes-page-loaded-1600x1000.png`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/live-processes-page-loaded-snapshot.md`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Workspace graph and analytics projections.
- Live Processes dashboard over `LiveProcessSnapshot`.
- Correct `Live 1h`, `1 day`, `7 days`, and `30 days` time-window filtering.
- Process filter and refresh behavior using snapshot cache/freshness metadata.
- Live incident action wiring to operator commands.

## Dependency Impact

- SB27 project-scoped live/process integration depends on these live/history query semantics.
- SB28 final regression depends on proving the current stale-event defect is fixed.

## Validation Depth

- Projection tests for time-window boundaries and active-run inclusion.
- Snapshot cache freshness/lag tests.
- Component tests for live dashboard tabs and filters.
- Playwright proof for `/processes/live` with history window changes and incident action visibility.

## Refactoring Review Checkpoint

- Keep component rendering separate from projection loading and command dispatch.
- Keep projection client code out of low-level visual components.
- Split large components or services before handoff if they combine unrelated workflow areas.
- Verify UI code does not reference runtime internals, EF runtime entities, or old observation services.

## Performance Antipattern Notes

- Read `architecture/19-dotnet-performance-guardrails.md` and `validation/05-dotnet-performance-antipattern-checklist.md` before creating or modifying C# hot-path code.
- Record exact performance scan counts in the execution report when this subbundle changes runtime, dispatcher, manager, projection, template, Git, adapter, persistence, or UI service code.
- Do not introduce sync-over-async, unbounded event/projector queues, per-call `HttpClient`, per-call `JsonSerializerOptions`, load-all UI queries, or LINQ-heavy hot paths without a recorded mitigation and proof.
## Implementation Steps

1. Bind graphs and analytics tabs to projection queries.
2. Rebuild Live Processes dashboard from live snapshot projections.
3. Implement history window and process filters at query boundary.
4. Implement refresh behavior without forcing runtime recomputation.
5. Wire live incident actions to operator commands.
6. Add tests and Playwright proof.
7. Record story coverage for US-044 through US-048.

## Do Not Do

- Do not mix historical events outside the selected window into live query results.
- Do not block runtime execution while refreshing UI snapshots.
- Do not compute cost/tool analytics from raw logs in the UI.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Graphs and analytics render from projections.
- [ ] Live dashboard counters and activity render from snapshot cache.
- [ ] Time-window filtering is correct and tested.
- [ ] Refresh behavior uses snapshot/query contracts.
- [ ] Browser proof exists.

## Proof Required

- Projection/cache/component test output.
- Playwright live dashboard screenshot evidence for multiple time windows.
- Story coverage table for US-044 through US-048.

## Browser Validation Logging

- Required. Capture `/processes/live`, selected time windows, process filter, refresh action, screenshots, and console/network summary.

## Progression Gate

- SB27 may start after live/history query semantics and dashboard proof are stable.

## Suggested Agent Prompt

Execute SB26 from `codex/bundles/process-module-architecture-v3/subbundles/26-analytics-graphs-live-processes-dashboard-snapshot-cache-and-time-windows`. Rebuild analytics and Live Processes over snapshot projections and prove time-window correctness.

## Handoff Notes For Next Bundle

Record project-scoped filter behavior and API/tool projection fields needed by SB27.
