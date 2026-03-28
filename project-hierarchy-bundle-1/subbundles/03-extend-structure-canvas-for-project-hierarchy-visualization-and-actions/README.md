# Extend structure canvas for project hierarchy visualization and actions

## Status

- `Completed`

## Objective

- Project the new project hierarchy into `/projects/{id}/structure` so the current project, its direct parents, its direct child projects, and its extra-parent context are all visible and actionable from the canvas.

## Covered Inputs

- `R009`
- `R010`
- `R011`
- `R012`
- `R013`
- UI slice of `R015`
- Raw notes `N007`, `N008`, `N009`, `N010`, `N011`, `N012`

## Prerequisites

- `01-model-project-hierarchy-and-persistence-foundation` completed and trusted.
- Label and interaction language from subbundle 02 reviewed so related-project UX stays consistent across surfaces.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureGraphAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeQuickActions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.Workflows.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureGraphAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\ProjectWorkbenchServiceIntegrationTests.cs`

## Deliverables

- Related-project nodes for direct children and direct parents on the structure canvas.
- Visible subdued nodes for extra parents of displayed child projects.
- Node actions for opening related project structure canvases in new tabs.
- Add/reconnect relation flows for project hierarchy from the canvas.
- Automated coverage for projection, styling, and actions.

## Dependency Impact

- This is a critical UI foundation. Weak proof here would make the final closure meaningless because one of the core requested surfaces would still be uncertain.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Extend workbench projection so related projects appear as typed related-project nodes and links.
2. Add metadata or subtype hooks that let the graph adapter render secondary-parent nodes in a subdued visual style.
3. Extend the action catalog and page workflows for add-subproject, reconnect, and open-related-project-in-new-tab behavior.
4. Add component and integration coverage for hierarchy-specific canvas behavior.
5. Prove the result in a real browser, including visible subdued parent nodes and new-tab actions.

## Scope Exceptions

- This phase does not attempt to render the entire transitive project graph on one canvas. It must render the current project's immediate hierarchy neighborhood clearly and make deeper traversal explicit through new-tab opening.

## Do Not Do

- Do not regress existing non-hierarchy node behavior on the structure page.
- Do not fake multi-parent support by duplicating the same project into unrelated ad-hoc nodes with no route/action contract.
- Do not close this phase on component tests alone.

## Acceptance Checklist

- Direct child projects of the current project are visible as project nodes.
- Direct parent projects of the current project are visible.
- When a displayed child has another parent, that other parent appears as a subdued related-project node.
- The subdued node still supports double-click or explicit action opening in a new tab.
- The user can add a subproject relation from the canvas.
- The user can reconnect a related project beneath another parent from the canvas.
- Existing structure-page quick actions for non-related-project nodes still work.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProjectStructurePageTests|FullyQualifiedName~ProjectStructureGraphAdapterTests"`
- `dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter "FullyQualifiedName~ProjectWorkbenchServiceIntegrationTests"`
- Headed Playwright MCP validation on `/projects/{id}/structure`
- Desktop screenshot showing the hierarchy neighborhood
- Desktop screenshot showing the subdued extra-parent node state
- Narrower-width screenshot after the desktop pass is stable

## Browser Validation Logging

- Route: `http://127.0.0.1:5188/projects/{id}/structure`
- Viewports: `1600x1000`, `1280x900`
- Required Playwright MCP actions:
- open the structure route for a project with at least one parent, one child, and one multi-parent child
- verify child and parent project nodes are visible
- open the quick action or double-click flow for a related project and verify the new-tab route
- add a subproject relation or verify the dedicated add relation affordance
- reconnect a related project beneath another parent and verify the resulting visual state
- confirm the subdued extra-parent node is still readable and obviously secondary
- Required screenshots:
- `C:\repositories\CanDoItAll\output\playwright\project-hierarchy\subbundle-03-structure-desktop.png`
- `C:\repositories\CanDoItAll\output\playwright\project-hierarchy\subbundle-03-structure-extra-parent.png`
- `C:\repositories\CanDoItAll\output\playwright\project-hierarchy\subbundle-03-structure-narrow.png`

## Progression Gate

- The targeted component and integration tests pass.
- Browser proof shows direct parents, direct children, and subdued extra-parent nodes clearly.
- A dependent-flow smoke proves that opening a related project's structure in a new tab works while the current canvas remains intact.

## Suggested Agent Prompt

```text
Implement subbundle 03 only. Project the project hierarchy into the structure canvas, add related-project node actions for new-tab opening and relation editing, render extra parents as visibly secondary nodes, and prove the result with component tests, integration tests, and a real Playwright browser pass with screenshots.
```
