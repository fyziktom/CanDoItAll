# SB09 UI Rebuild

## Status

Planned.

## Objective

Rebuild Process UI against the new application services and projection read models while preserving the current UI/UX direction.

## Covered Inputs

- REQ-005
- REQ-030
- REQ-033
- REQ-041

## Prerequisites

- SB04 complete.
- SB08 complete.
- SB07 complete for manager/artifact/subprocess UI.

## Exact Source References

- `repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspace.razor`
- `repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor`
- `repo://src/CanDoItAll.Modules.Processes/Canvas`

## Deliverables

- Process workspace against new definition and runtime projections.
- Live Processes view against snapshot cache.
- Template catalog and override/conflict UI.
- Branch definition selection/customization UI.
- Artifact ledger and recovery incident UI.
- Manager incident/escalation UI.
- Git status/diff/conflict components composed into Process UI.

## Dependency Impact

- Final migration proof depends on UI.

## Validation Depth

- High.
- Browser-visible.

## Implementation Steps

1. Recreate workspace shell with existing UX direction.
2. Rebind definition tree and canvas to new projections.
3. Rebind runs/live/history tabs to snapshots.
4. Add template override/conflict flows.
5. Add branch/switch editor improvements.
6. Add artifact ledger and manager incident surfaces.
7. Add Git UI components where relevant.
8. Validate desktop and narrower viewport layouts.

## Scope Exceptions

Do not rebuild old internal service dependencies. UI must use new application services.

## Do Not Do

- Do not read EF entities directly.
- Do not infer process truth in Razor components.
- Do not regress current useful UX concepts.

## Acceptance Checklist

- Process definitions can be viewed and edited.
- Runs can be launched and monitored.
- Live/history time filters behave correctly.
- Template conflict UI can resolve a conflict.
- Branch editor supports generic and domain-provided definitions.
- Artifact recovery incidents are visible and actionable.

## Proof Required

- Component tests.
- Playwright tests for key flows.
- Screenshots for workspace, live dashboard, template conflict, branch editor, artifact ledger, and manager incident.
- Browser validation analytics.

## Browser Validation Logging

- Record route, viewport, actions, assertions, screenshot paths, and result under `proof/SB09/browser-validation.md`.

## Progression Gate

- SB10 final proof cannot proceed until UI flows are browser-validated.

## Suggested Agent Prompt

Rebuild the UI on projections. Preserve the current Process workspace and Live Processes direction, but do not preserve backend coupling.
