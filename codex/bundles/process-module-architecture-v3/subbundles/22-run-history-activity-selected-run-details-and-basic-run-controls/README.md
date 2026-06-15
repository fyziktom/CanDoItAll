# SB22 Run History, Activity, Selected Run Details, And Basic Run Controls

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild run history, activity view, selected run summary, filters, tags, updated-time filtering, status details, and basic authorized controls such as stopping a blocked run.

## Covered Inputs

- REQ-002, REQ-026 to REQ-030, REQ-051, REQ-052.
- US-030 through US-032.
- AC-006, AC-018, AC-020, AC-021, AC-039, AC-040.

## Prerequisites

- SB21 launch creates governed runs.
- SB10 projection contracts and SB07 runtime state transitions complete.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsLifecycleSection.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceRunsActiveSection.razor`
- `repo://tests/CanDoItAll.Tests.Integration/ProcessRuntimeReadQueryServiceTests.cs`
- `repo://codex/bundles/process-module-architecture-v3/evidence/ui-current-state/processes-runs-tab-1600x1000.png`

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- New Process application/projection projects introduced by upstream backend subbundles.
- `tests/CanDoItAll.Tests.Components`
- `tests/CanDoItAll.Tests.Playwright`
- Subbundle proof directory for screenshots, snapshots, and execution report artifacts.

## Deliverables

- Run history projection UI with text, state, operating mode, updated-time, and tag filters.
- Selected run summary projection including manager, subprocess depth, attempts, approvals, missing artifacts, dead letters, diagnostics, and recovery recommendation.
- Basic authorized run control commands with receipts.
- Component and Playwright proof.

## Dependency Impact

- SB23 runtime execution view depends on selected run context and run detail projection.
- SB24 operator control depends on incident/dead-letter summaries exposed here.

## Validation Depth

- Projection query tests for filters and selected run detail.
- Runtime state command tests for authorized stop behavior.
- Playwright proof for run filtering and selected run summary.

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

1. Bind run history UI to run history projection queries.
2. Implement filters and selected run state.
3. Render selected run status, diagnostics, and recovery summaries.
4. Wire authorized basic run controls.
5. Add tests and Playwright proof.
6. Record story coverage for US-030 through US-032.

## Do Not Do

- Do not compute run status from raw logs in the UI.
- Do not expose raw diagnostics directly.
- Do not implement runtime step operations or operator console actions in this bundle.

## Stop And Report Conditions

- Stop if required projection fields are missing and would force direct runtime or persistence access from UI.
- Stop if preserving the current UX requires reviving old dispatcher/runtime behavior.
- Stop if browser proof cannot be captured for an owned browser-facing story.
- Stop if a story appears to require removal or major UX replacement without explicit user approval.

## Acceptance Checklist

- [ ] Run history filters work at query/projection boundary.
- [ ] Selected run summary matches projection state.
- [ ] Authorized stop control works for blocked runs.
- [ ] Browser proof exists.

## Proof Required

- Projection/runtime/component test output.
- Playwright run history screenshot evidence.
- Story coverage table for US-030 through US-032.

## Browser Validation Logging

- Required. Capture runs tab, filters, selected run, control action, screenshot, and console/network summary.

## Progression Gate

- SB23 may start after selected run context and run detail projections are stable.

## Suggested Agent Prompt

Execute SB22 from `codex/bundles/process-module-architecture-v3/subbundles/22-run-history-activity-selected-run-details-and-basic-run-controls`. Rebuild run history and selected run controls over projections.

## Handoff Notes For Next Bundle

Record runtime canvas fields and active execution gaps needed by SB23.
