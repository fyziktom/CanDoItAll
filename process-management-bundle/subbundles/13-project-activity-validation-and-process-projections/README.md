# 13 Project, Activity, Validation, And Process Projections

## Status

- `Ready`

## Objective

- Integrate process context into project, activity, validation, automation, and test surfaces while keeping those integrations projection-only and typed.

## Covered Inputs

- `REQ-013`
- `REQ-016`
- `REQ-022`
- Legacy features `PRM-F10` and `PRM-F11`

## Prerequisites

- `12-post-implementation-bundle-phase02-generation`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F10-project-workbench-and-shell-projections\README.md`
- `C:\repositories\CanDoItAll\process-management-bundle\03-subbundles\PRM-F11-activity-automation-validation-and-testlab-hooks\README.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Activity`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Validation`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.TestLab`

## Deliverables

- Typed project-to-process links and projection surfaces.
- Activity, validation, automation, and test hooks that preserve canonical ownership boundaries.
- Projection rules that keep project and workbench surfaces useful without letting them become alternate process stores.
- Initial management-readable process entry points inside project-linked UX.

## Dependency Impact

- Live overlays and management UX depend on these integrations being projection-only.
- If this subbundle leaks canonical state into projections, later bridge and management surfaces will encode the wrong ownership model.

## Validation Depth

- `UI, component-test, integration-test, and browser-proof`

## Implementation Steps

1. Add typed projection links from processes into project and activity surfaces.
2. Connect validation, automation, and test hooks without tight coupling.
3. Expose process entry points in project UX without duplicating process truth.
4. Prove the resulting routes and projections in browser tests.

## Scope Exceptions

- Full executive dashboards remain phase 04 work, but phase 03 must expose stable navigation and operational hooks.

## Do Not Do

- Do not copy canonical process state into project or workbench storage.
- Do not make activity or automation hooks depend on the intelligence lake.
- Do not hide process-linked context in loose string identifiers when typed references can exist.

## Acceptance Checklist

- Project and activity surfaces can reference processes through typed links.
- Validation and test hooks can attach process evidence without owning process truth.
- Projection surfaces stay explicit and read-only with respect to canonical process semantics.
- Browser-visible process-entry routes are validated on large screens.

## Proof Required

- Integration tests for project and hook wiring.
- Browser proof for project-linked process routes and any new projection pages.
- Screenshot review proving projection surfaces remain compact and coherent.

## Browser Validation Logging

- Route:
  project-linked process entry route
- Route:
  any activity or validation page changed by this work
- Viewport:
  `1920x1080`
- Viewport:
  `1600x900`
- Evidence:
  Playwright actions plus screenshots recorded in the execution report

## Progression Gate

- Later bridge and management UI work may continue only when projections are clearly typed, browser-validated, and proven not to become canonical stores.

## Suggested Agent Prompt

```text
Implement only the projection and cross-module integration slice. Link processes into project, activity, validation, automation, and test surfaces through typed projections and prove the browser-visible routes on large screens.
```
