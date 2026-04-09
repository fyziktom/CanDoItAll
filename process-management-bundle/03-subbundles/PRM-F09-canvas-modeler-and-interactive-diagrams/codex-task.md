# Codex task — PRM-F09

Implement **Canvas modeler and interactive diagrams** inside the uploaded CanDoItAll solution.

## Constraints

- Treat `CanDoItAll.Modules.Processes` as the canonical owner for process-management behavior.
- Do not move canonical process semantics into Workbench metadata.
- Keep all code comments in English.
- Preserve buildability for the current solution layout.
- Do not block Wave 1 designer delivery on later Wave 2 handoff chrome.

## Required outputs

- Code changes for this feature
- Matching tests
- Migration updates if persistence changes
- A short implementation note describing what changed and how it was verified

## Done definition

This task is done when:

- Users can create and edit process nodes and transitions from an interactive canvas surface.
- Diagram layout persists independently from the canonical graph semantics.
- Phase grouping and actor grouping can be represented without forcing Workbench to be the source of truth.
- Wave 1 designer delivery is not blocked by later handoff-label chrome; handoff visuals can deepen in Wave 2.
- The design leaves room for labeled transitions and swimlane extensions where CanvasLib needs them.

## Recommended first files to touch

- `src/CanDoItAll.Modules.Processes/CanvasAdapters/ProcessCanvasGraphAdapter.cs (new)`
- `src/CanDoItAll.Modules.Processes/Pages/ProcessDesignerPage.razor (new)`
- `src/CanDoItAll.Modules.Processes/ProcessActorTemplateBridge.cs (new or reused)`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchSurface.cs`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchNode.cs`
- `src/CanDoItAll.Components.CanvasLib/Canvas/Workbench/CanvasWorkbenchEvents.cs`
- `tests/CanDoItAll.Tests.Components/ProcessDesignerPageTests.cs (new)`
- `tests/CanDoItAll.Tests.Playwright/ProcessModelingFlowTests.cs (new)`
