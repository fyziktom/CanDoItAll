# Add projects page hierarchy discovery and modal navigation

## Status

- `Completed`

## Objective

- Surface the new hierarchy on `/projects` so the user can filter by related project context, find parents, open direct subprojects from a card, and recursively drill into deeper subprojects without losing the current page workflow.

## Covered Inputs

- `R005`
- `R006`
- `R007`
- `R008`
- UI slice of `R015`
- Raw notes `N005`, `N006`, `N011`, `N012`

## Prerequisites

- `01-model-project-hierarchy-and-persistence-foundation` completed and trusted.
- The hierarchy query contract is stable enough that the page does not need to invent client-side graph joins.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\ProjectModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectsPageTests.cs`

## Deliverables

- Hierarchy-aware filter and lookup behavior on `/projects`.
- A per-card hierarchy affordance that opens direct subprojects.
- Recursive drill-in across subproject cards.
- Visible multi-parent cues for subprojects that belong elsewhere too.
- Preservation of existing project actions from the hierarchy views.

## Dependency Impact

- This phase closes the first major user-visible hierarchy story. If it is wrong, the raw-note closure for `/projects` is invalid and the final regression pass cannot be honest.

## Validation Depth

- `UI, component-test, and browser-proof`

## Implementation Steps

1. Extend the page-facing project summaries or view models with the hierarchy metadata needed for parent/child discovery.
2. Add hierarchy filter state and parent lookup to the Projects page command bar or nearby surface.
3. Add the project-card hierarchy affordance and recursive subproject modal flow.
4. Surface multi-parent indicators and preserve existing related project actions from the modal cards.
5. Add component coverage and then prove the final layout and behavior in a real browser.

## Scope Exceptions

- This phase does not add hierarchy nodes to the structure canvas; that belongs to subbundle 03.

## Do Not Do

- Do not replace the existing overview/editor modal workflow with a separate hierarchy admin page.
- Do not introduce string-based modal state that the route or existing page logic cannot reason about.
- Do not weaken the recursive-drill requirement into a flat child list only.

## Acceptance Checklist

- The user can filter the visible cards to a selected project's direct subprojects.
- The user can identify at least one parent project for a child project from `/projects`.
- Each project card exposes a hierarchy affordance that opens direct subproject cards.
- The subproject view supports recursive drill-in to another project's children.
- Multi-parent state is visible on a child card when relevant.
- Existing dashboard, structure, calendar, or detail actions still remain available from the page workflow.

## Proof Required

- `dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter "FullyQualifiedName~ProjectsPageTests"`
- Headed Playwright MCP validation on `/projects`.
- Desktop screenshot of the hierarchy-enhanced card grid.
- Desktop screenshot of the opened subproject modal.
- Narrower-width screenshot after the desktop pass is stable.

## Browser Validation Logging

- Route: `http://127.0.0.1:5188/projects`
- Viewports: `1600x1000`, `1280x900`
- Required Playwright MCP actions:
- open `/projects`
- create or load a hierarchy fixture with at least one multi-parent child
- filter the list to a selected project's subprojects
- open the subproject modal from a card
- drill into one nested subproject view
- verify the multi-parent cue and preserved project actions
- Required screenshots:
- `C:\repositories\CanDoItAll\output\playwright\project-hierarchy\subbundle-02-projects-desktop.png`
- `C:\repositories\CanDoItAll\output\playwright\project-hierarchy\subbundle-02-projects-subprojects-modal.png`
- `C:\repositories\CanDoItAll\output\playwright\project-hierarchy\subbundle-02-projects-narrow.png`

## Progression Gate

- The targeted component tests pass.
- Browser proof shows the hierarchy discovery flow is readable and coherent on both planned widths.
- Recursive drill-in and multi-parent cues are proven, not assumed.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Extend `/projects` with hierarchy discovery, parent lookup, and a recursive subproject modal launched from each card. Keep the existing card/modal workflow coherent, preserve current project actions, and prove the result in component tests plus a real Playwright browser pass with screenshots.
```
