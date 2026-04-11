# Process Branch Node Authoring And Mapping

## Status

- `Ready`

## Objective

- Rework process-side authoring and mapping so the latest requested behavior is real: left-click connection authoring between process nodes, exact badge-backed port circles including the router-side decision-role badge, honest many-to-many handling, and persisted layout for derived nodes.

## Covered Inputs

- `N001` Add branch via step context menu and create a connected branch node.
- `N002` Branching must be its own node.
- `N003` One route per matched outcome plus default and error.
- `N004` Downstream process nodes connect to branch outputs.
- `N005` Decision maker supports input from a role-definition node.
- `N011` Left click starts connector authoring and left click confirms it on a target circle.
- `N012` Connector circles must sit exactly on their badges and none may be missing.
- `N013` Many-to-many routing semantics must be supported or blocked honestly.
- `N014` Moved derived nodes must persist and not snap back after later interactions.

## Prerequisites

- `subbundles/01-scenario-definition-and-live-gap-reconciliation` must be `Completed` and trusted.
- `subbundles/02-advanced-canvas-node-contract` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasBranching.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSurfaceFactoryTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`

## Deliverables

- Process canvas projection that exposes the correct badge-backed circles for steps, roles, and branch routers.
- Left-click process connection authoring that maps the clicked source circle and clicked target circle to the correct process-side connection.
- Process-side many-to-many support for join-style inputs, or an explicit documented blocker if the current domain cannot support it canonically.
- Canonical persisted layout handling for moved role, router, and other derived nodes.

## Dependency Impact

- Later seeded scenarios and closure proof depend on this phase to make the new authoring and persistence behavior real.
- Weak proof here would let screenshots look improved while canonical process data still overwrites connections or loses node positions.

## Validation Depth

- `Critical product foundation`

## Implementation Steps

1. Inspect the current process-side connection mapping and determine whether many-to-many requires an additive domain change.
2. Update the surface factory and workspace authoring flow so every required badge-backed port has the correct connector circle, including the router-side decision-role badge.
3. Change process-specific connection completion logic to use the left-click source and target port identities.
4. Implement canonical persistence for moved derived nodes and verify that later interactions no longer snap them back.
5. Add or extend focused process tests for routed links, many-to-many handling, and layout persistence.
6. Prove the behavior in the browser on `/processes`.

## Do Not Do

- Do not keep right-click-only proof and call the gesture work complete.
- Do not draw many-to-many joins in the browser while still persisting only one upstream dependency.
- Do not fix layout persistence only in browser memory.

## Acceptance Checklist

- Adding a branch still creates a separate branch node connected to the selected step.
- Left click on a process-node connector circle starts a draft and left click on a compatible target circle completes it.
- The router-side decision-role badge exposes its own visible connector circle.
- Many-to-many join behavior is either supported canonically and tested or blocked honestly with documented proof.
- Moved role or router nodes remain in place after a later editor interaction or surface rebuild.

## Proof Required

- Focused process component or module tests for connection mapping and persisted movement.
- Browser proof on `/processes` showing left-click authoring, badge alignment, and movement stability.

## Browser Validation Logging

- Route: `/processes`
- Viewports: `Large-screen desktop` and `1280x800`
- Playwright MCP actions: navigate, initiate connection from a source circle, complete it on a target circle, move nodes, trigger an editor or rebuild interaction, capture screenshots
- Expected evidence path: process-authoring screenshots recorded in `reviews/01-execution-report.md`

## Progression Gate

- `subbundles/04-software-development-branching-examples-and-regression-coverage` may continue only after left-click connection authoring, badge completeness, and layout persistence are proven in both code and browser evidence.

## Suggested Agent Prompt

```text
Implement this subbundle only. Rework process-side branch-node mapping and workspace authoring so left-click connector circles create the intended process links, every required badge-backed port has a visible circle, many-to-many joins are supported canonically or blocked honestly, moved derived nodes persist correctly, and the behavior is proven on /processes.
```
