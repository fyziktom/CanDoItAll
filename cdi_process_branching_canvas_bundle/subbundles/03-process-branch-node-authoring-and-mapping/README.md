# Process Branch Node Authoring And Mapping

## Status

- `Completed`

## Objective

- Project real branch nodes into the process workspace, create them from right-click branch actions, and wire role inputs and outcome ports to downstream nodes.

## Covered Inputs

- `N001` Right-click branch creation must create a connected branch node.
- `N002` Branching must be its own node.
- `N003` One route per matched outcome plus default and error.
- `N004` Downstream process nodes connect to branch outputs.
- `N005` Decision maker supports input from a role-definition node.

## Prerequisites

- `subbundles/01-scenario-definition-and-live-gap-reconciliation` must be `Completed` and trusted.
- `subbundles/02-advanced-canvas-node-contract` must be `Completed` and trusted.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessDefinitionModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessRuntimeModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessesService.Runtime.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.Canvas.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessCanvasSelectionPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepEditorForm.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessStepBranchOutcomeEditor.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSurfaceFactoryTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessCanvasSelectionPanelTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessStepEditorFormTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProcessWorkspaceTests.cs`

## Deliverables

- Process canvas projection that emits a separate branch node when branching exists or is created.
- Right-click branch-node creation flow that connects the new node to the clicked step.
- Port mapping for matched outcomes, default, error, and decision-role input.
- Process-module regression tests for branch-node projection and authoring behavior.

## Dependency Impact

- Later scenario and closure work depends on this phase to prove the real requested behavior on the canvas.
- Weak proof here would let scenario seeds exist while the actual branch-node authoring flow is still wrong.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Decide the minimal process-side representation for a branch node without breaking the current process model.
2. Update the process canvas surface factory to emit advanced nodes and advanced links for branch semantics.
3. Update right-click and selection-panel flows so adding a branch creates the projected branch node and connects it to the selected step.
4. Surface role-definition input routing for decision-maker branches when the underlying data exists.
5. Add component tests for branch-node projection and authoring.
6. Prove the behavior in the browser on `/processes` with desktop and narrower-width screenshots.

## Scope Exceptions

- If the current persisted model cannot represent default or error routes cleanly, document the exact gap in `analysis/03-architecture-troubles-log.md` and reopen the bundle instead of faking it.

## Do Not Do

- Do not retrofit every process node into an advanced node when only branching needs it.
- Do not hide missing role-input behavior behind non-connected badges or summary text.

## Acceptance Checklist

- Adding a branch from the canvas creates a separate branch node connected to the clicked step.
- The branch node exposes one connectable output per explicit outcome plus default and error outputs.
- The branch node can expose a role-definition input when the decision role exists.
- Downstream steps map to the correct branch-node outputs.
- Existing non-branching process steps still render correctly.

## Proof Required

- Focused process component tests for branch-node projection and authoring.
- Browser proof on `/processes` at `1600x900` and `1280x800`.
- Screenshots that clearly show the separate branch node, labeled ports, and readable curves.

## Browser Validation Logging

- Route: `/processes`
- Viewports: `1600x900` and `1280x800`
- Playwright MCP actions: navigate, locate or seed a process, invoke right-click branch creation, inspect the new node, capture screenshots
- Expected evidence path: desktop and narrower screenshots recorded in `reviews/01-execution-report.md`
- Screenshot review questions: is the branch node visually separate, are ports readable, do curves overlap or clip, is the role input visible when expected, and does the screen stay coherent with the app’s visual system

## Progression Gate

- Subbundle `04` may continue only after the browser pass proves right-click branch creation, readable multi-port rendering, and no obvious regression to non-branching steps.

## Suggested Agent Prompt

```text
Implement this subbundle only. Use the new additive CanvasLib port contract to project a real branch node in the process workspace, wire right-click branch creation to it, connect matched outcome plus default and error ports, surface role-definition input when applicable, and prove the behavior on /processes with screenshots.
```

## Closure Notes

- Process canvas projection, selection, and editor flows now treat the branch router as its own node while preserving legacy single-anchor nodes.
- Focused component tests passed for router-port mapping, runtime projection, and role-selection UI.
- Browser proof on `/processes` showed the separate router node, the role-to-router input curve, and explicit output lanes including `Default` and `Error`.
