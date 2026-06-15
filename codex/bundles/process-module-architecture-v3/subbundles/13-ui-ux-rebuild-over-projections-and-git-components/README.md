# SB13 Process UI/UX Rebuild Over Projections And Git UI Components

## Status

Future implementation package; prepared by architecture bundle v3; not executed in v3.

## Objective

Rebuild the Process UI over application/projection contracts while preserving useful UX direction: Live Processes, Process Workspace, definition/runtime canvas, run details, timeline/dialogs, template catalog/editor, launch flows, manager incidents, escalations, and generic Git UI components.

## Why This Bundle Exists

The UI is a useful anchor, but current UI services are too close to runtime internals. This bundle keeps the experience while removing runtime/EF coupling.

## Covered Inputs

- REQ-030.
- REQ-041.
- v3 UI projection inventory and template/history compatibility decisions.

## Context Reset: Read These First

- SB10 and SB12 execution reports.
- `architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`
- `architecture/08-monitoring-events-snapshots-and-ui-projections.md`
- `architecture/09-template-git-versioning-and-migrations.md`
- `architecture/17-runtime-history-migration-and-readonly-compatibility.md`

## Exact Source References

- `repo://codex/bundles/process-module-architecture-v3/architecture/15-ui-ux-projection-contracts-and-reuse-plan.md`
- `repo://codex/bundles/process-module-architecture-v3/architecture/08-monitoring-events-snapshots-and-ui-projections.md`
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `repo://src/CanDoItAll.Modules.Processes/Canvas`

## Source Evidence To Use

- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `repo://src/CanDoItAll.Modules.Processes/Canvas`
- `repo://src/CanDoItAll.Modules.Processes/Templates`
- SB01 UI archive.

## Prerequisites

- SB10 projection contracts complete.
- SB12 compatibility decisions complete.
- Git wrapper available.

## In Scope

- Projection-first UI services.
- Live Processes dashboard over projections.
- Process Workspace over projections.
- Definition canvas projection rendering.
- Runtime canvas projection rendering.
- Run detail/timeline/dialogs.
- Template catalog/editor over canonical JSON services.
- Launch/run start flows through application services.
- Manager incident/escalation views.
- Generic Git UI components.
- Component tests.
- Playwright smoke paths.

## Out Of Scope

- Do not alter runtime state directly from UI.
- Do not query runtime EF entities.
- Do not implement new runtime behavior to satisfy UI shortcuts.
- Do not complete final E2E hardening; that is SB14.

## Target Projects / Files

- `src/CanDoItAll.Modules.Processes`
- `src/CanDoItAll.Components.Git`
- UI tests.

## Deliverables

- Projection-first Process UI.
- Generic Git UI components.
- Component tests.
- Playwright smoke proof.

## Expected Deliverables

- UI preserves current workflow direction.
- UI displays projection freshness/lag.
- UI labels legacy read-only history.
- UI uses restricted links for raw diagnostics.

## Dependency Impact

- SB14 validates end-to-end flows and hardening.

## Validation Depth

- Validate with component tests, Playwright tests, UI dependency scans, restricted diagnostic rendering tests, template conflict UI tests, and UI projection review.

## Architecture Invariants That Must Hold

- UI references application/projection contracts only for Process data.
- UI does not compute runtime truth.
- UI does not treat Markdown/Mermaid as canonical.
- UI does not call dispatcher/runtime internals.

## Implementation Steps

1. Build generic Git UI components.
2. Replace Process data-loading services with projection application services.
3. Rebuild Live Processes dashboard.
4. Rebuild workspace/run/canvas/template views.
5. Add manager incident/escalation views.
6. Add component tests.
7. Add Playwright smoke tests.
8. Run dependency and old-symbol scans.

## Refactoring Review Checkpoint

- Split UI rendering from presenter/data-loading services.
- Keep components focused on rendering and explicit user actions.
- Verify no component queries DbContext/runtime internals.

## Required Tests / Proof

- Component tests.
- Playwright smoke tests for Live Processes, workspace, canvas, template conflict, manager incident.
- UI dependency tests.
- Restricted diagnostic rendering tests.

## Search Proof

- Search UI for EF runtime entity references.
- Search UI for old observation service references.
- Search UI for dispatcher/runtime internals.

## Stop And Report Conditions

- Stop if UI cannot render required data without querying runtime internals.
- Stop if projection contracts are missing critical fields.
- Stop if old UI service must be retained to preserve behavior.

## Do Not Do

- Do not query EF runtime entities from UI.
- Do not compute runtime truth in components.
- Do not call dispatcher/runtime internals from UI.
- Do not make Markdown/Mermaid canonical.

## Acceptance Checklist

- [ ] UI uses projection services.
- [ ] Live Processes works over projections.
- [ ] Canvas renders projections.
- [ ] Template conflict UI works.
- [ ] Git UI components work.
- [ ] Component and Playwright tests pass.

## Proof Required

- Test output.
- Playwright evidence.
- UI projection review.
- Dependency scan.

## Browser Validation Logging

- Required. Record route, viewport, user actions, assertions, screenshots, console/network issues, and result for each smoke path.

## Progression Gate

- SB14 may start after UI smoke tests and projection-only dependency scans pass.

## Suggested Agent Prompt

Execute SB13 from `codex/bundles/process-module-architecture-v3/subbundles/13-ui-ux-rebuild-over-projections-and-git-components`. Rebuild Process UI over projections and generic Git components. Do not query runtime internals.

## Handoff Notes For Next Bundle

Record UI routes, Playwright proof paths, remaining UX gaps, projection gaps, and final E2E scenarios for SB14.
