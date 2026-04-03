# Phase 02 canvas toolbar modes and dependency authoring UX

## Status

- `Completed`

## Objective

- Deliver the requested canvas toolbar tool cluster and interactive dependency and delete workflows without breaking existing node drag behavior.

## Covered Inputs

- `N003`
- `N004`
- `N005`
- `N006`
- `N007`
- `RQ-003`
- `RQ-004`
- `RQ-005`
- `RQ-006`
- `RQ-007`
- `RQ-008`

## Prerequisites

- `subbundles/01-phase-01-models-persistence-and-mcp-dependency-surfaces`

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeEditing.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.SelectionPanel.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureToolbarActions.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Workbench\CanvasWorkbench.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchSurface.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\06-canvas-renderers.js
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\wwwroot\js\runtime\workbench\07-runtime-entry.js
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs
- C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\AppSmokeTests.ProjectStructureCanvasFeedbackBundle.cs

## Deliverables

- Top-toolbar tool cluster for select, dependency, and delete.
- Dependency-preview interaction that starts from the selected node and completes on second-node click.
- Delete-mode hover and highlight behavior for nodes and links, plus safe confirmation for risky node deletion.
- Component or runtime coverage for tool state and user interaction semantics.

## Dependency Impact

- Phase 04 browser proof depends on this phase to expose the requested user workflow.
- Weak tool-state or delete semantics here would invalidate any end-to-end screenshots because the visible flow would be wrong.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add a toolbar tool cluster and corresponding page or runtime state for select, dependency, and delete modes.
2. Extend canvas interaction logic so dependency preview and completion coexist with normal node dragging.
3. Add link hover hit-testing and highlight plus delete handling for links and nodes.
4. Add node-delete confirmation rules for multiply-connected cases.
5. Add component or runtime validation and prepare browser automation hooks used in Phase 04.

## Scope Exceptions

- Final fresh-SQLite browser proof lands in Phase 04.
- Dependency-analysis and Mermaid export logic land in Phase 03.

## Do Not Do

- Do not introduce a canvas-only dependency model that bypasses the workbench service layer.
- Do not remove standard drag behavior while dependency mode is active.
- Do not close this phase on screenshots alone without at least one automated assertion path.

## Acceptance Checklist

- Toolbar exposes the requested three tools and reflects current mode.
- Selecting dependency mode from a selected node shows an obvious pending-link state.
- Clicking another node creates the dependency while drag remains available elsewhere.
- Delete mode highlights hovered nodes or links and can remove both types safely.
- Deleting heavily-connected nodes prompts for confirmation.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectStructurePageTests`
- Any targeted JS or runtime or page-state assertions added for delete and dependency modes.
- Preliminary browser sanity check if component proof cannot fully validate runtime hit-testing behavior.

## Browser Validation Logging

- Route: `/workbench/projects/{projectId}/structure`
- Viewports: `1600x900` required; add `1280x900` if toolbar layout changes.
- Required actions: toggle toolbar tools, begin dependency mode from a selected node, inspect preview state, enter delete mode, hover a link or node, and confirm any delete warning rules.
- Screenshot placeholders to log later: `evidence/project-structure-dependency-mode-desktop.png`, `evidence/project-structure-delete-mode-desktop.png`
- Screenshot review questions: is the active tool obvious, is the pending curve readable, is hover highlight strong enough, and is arrow direction visually clear?

## Progression Gate

- Do not let Phase 04 claim closure until the toolbar workflow exists and delete or highlight behavior is already believable from targeted proof.

## Suggested Agent Prompt

```text
Implement Phase 02 only.

Add the select, dependency, and delete toolbar tools and the requested canvas interactions.
Preserve left-drag node movement while dependency mode is active.
Do not close the phase until delete mode can target links as well as nodes.
```
