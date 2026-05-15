# 01-design-baseline-imagegen-and-route-inventory

## Status

- `Completed`

## Objective

- Establish the large-screen visual baseline and route-by-route proposal inventory before implementation edits begin.

## Covered Inputs

- RN-001 improve visual look and working space.
- RN-002 large-screen-only hard rule.
- RN-003 use the Economy Simulator visual concept.
- RN-008 analyze each page and get `imagegen` design proposals.
- RN-012 make the app professional and understandable for customer video recording.

## Prerequisites

- SB00-01 page-function inputs and proposal coverage gate passed.
- Bundle source screenshots are preserved under `inputs`.
- The app can be run locally or an explicit blocker is recorded before browser screenshots are attempted.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\reference-02-run-observation-page.png`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\reference-11-run-bus-tab.png`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inventories\01-scope-inventory.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inputs\page-inputs`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\analysis\03-imagegen-proposal-review.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\01-shell-tree-workspace-imagegen-proposal.png`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Pages\Home.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\WorkflowsPage.razor`
- `C:\repositories\CanDoItAll.Economy\src\CanDoItAll.Economy.Simulator.Components\Components\SimulationRunObservationPage.razor`
- `C:\repositories\CanDoItAll.Economy\src\CanDoItAll.Economy.Simulator.Components\Components\BusinessUnitTree.razor`

## Deliverables

- Baseline large-screen screenshot set for every route in `inventories/01-scope-inventory.md`.
- Route inventory updated with screenshot path, visual problem summary, target proposal, and imagegen proposal status.
- Page-input files cross-checked against runtime route states, tabs, dialogs, and baseline screenshots.
- Additional `imagegen` prompts or generated planning images for page/tab/dialog groups where existing proposals are not specific enough.
- Execution report rows seeded with baseline screenshot paths and open visual questions.

## Dependency Impact

- SB02, SB03, SB04, SB05, and SB06 depend on this baseline to know whether later screenshots actually improve the app.
- Weak or incomplete route inventory invalidates downstream claims that "each page" was analyzed.

## Validation Depth

- Critical UI foundation.

## Implementation Steps

1. Start the app through the repo's normal Blazor/dev workflow.
2. Use a large desktop viewport, recommended `1920x1080`, and capture each route listed in `inventories/01-scope-inventory.md`.
3. For project-scoped routes that need ids, use seeded or existing project data and record the chosen ids.
4. Add screenshot paths and concise visual findings to the inventory.
5. Compare baseline screenshots against `inputs/page-inputs` and accepted proposal assets.
6. Generate additional `imagegen` planning proposals only where a route, tab, or dialog needs a different composition from the accepted boards.
7. Update `reviews/01-execution-report.md` with baseline browser analytics.

## Scope Exceptions

- Do not tune or validate small/medium screen layouts in this phase.
- Do not edit product code in this phase.

## Do Not Do

- Do not replace browser proof with generated mockups.
- Do not skip pages because they look simple; mark them low-change instead.
- Do not start shell or page implementation until this gate passes.

## Acceptance Checklist

- Every route in the inventory has a baseline screenshot path or an explicit blocker.
- Every route has an initial target proposal.
- Every page/tab/dialog group has an accepted `imagegen` planning asset or explicit exception.
- Reference screenshots are preserved in `inputs`.
- Browser analytics rows are updated for this phase.

## Proof Required

- Large-screen screenshots under the bundle evidence path or another recorded proof path.
- Updated `inventories/01-scope-inventory.md`.
- Updated `reviews/01-execution-report.md`.
- No product code diff from this subbundle except bundle artifacts.

## Browser Validation Logging

- Route: every route listed in `inventories/01-scope-inventory.md`.
- Viewport: large desktop, recommended `1920x1080` or maximized headed browser.
- Actions: navigate to route, wait for stable layout, capture screenshot, note hidden ids or data blockers.
- Screenshots: record one baseline path per route.
- Review questions: where is working space wasted, what text can move to tooltip/dialog, what large list should become a tree, and what screenshot element should resemble the Economy reference.

## Progression Gate

- Downstream implementation may start only after all route rows have baseline/proposal coverage or explicit blockers accepted in the execution report.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Capture large-screen baseline screenshots for every route in the route inventory, update the route inventory with visual findings and design proposal status, add imagegen planning prompts/assets where needed, update the execution report, and stop before editing product UI code.
```
