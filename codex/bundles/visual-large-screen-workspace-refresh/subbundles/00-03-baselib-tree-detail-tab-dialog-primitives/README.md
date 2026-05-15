# 00-baselib-tree-detail-tab-dialog-primitives

## Status

- `Ready`

## Objective

- Build or extend reusable BaseLib primitives for tree/detail workspaces, dense tab bodies, inspector dialogs, compact metric strips, entity action toolbars, and compact empty/loading/error states before individual page repairs.

## Covered Inputs

- RN-001 improve visual look, working space, and clarity.
- RN-007 use maximum available width.
- RN-008 proposal coverage for tab contents and dialogs.
- RN-009 no own CSS; prefer BaseLib/Tailwind/component options.
- RN-010 use dialogs for too much information.
- RN-011 TreeView for projects/processes/workflows and large lists.
- RN-012 professional B2B readiness.

## Prerequisites

- SB00-01 page inputs and proposal coverage passed.
- Existing BaseLib components are reviewed before adding new abstractions.

## Exact Source References

- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\inventories\02-reusable-baselib-component-candidates.md`
- `C:\repositories\CanDoItAll\codex\bundles\visual-large-screen-workspace-refresh\evidence\design-proposals\pages\07-baselib-reusable-components-proposal.png`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\PageScaffold.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Lists\ListDetailShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TreeView.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\SecondaryTabs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\DialogScaffold.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\InspectorDialogLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Cards\SummaryTiles.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Cards\MetricCard.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Toolbar.razor`

## Deliverables

- Reusable `TreeDetailWorkspace` composition or extensions to `ListDetailShell`/`TreeView`.
- Dense tab workspace variant for fill-height desktop tab bodies.
- Inspector dialog scaffold/preset for dense context rail, main form, review panel, validation strip, and sticky footer.
- Compact metric/status strip variant based on existing summary/metric components.
- Entity action toolbar/filter pattern with icon actions, search, filters, overflow, and selection state.
- Component examples or tests proving layout stability and no text overlap at large desktop.

## Dependency Impact

- SB03, SB04, SB05, and added tab/dialog subbundles depend on these primitives.
- If these primitives are weak, page teams will add one-off Tailwind or custom CSS and violate the bundle constraint.

## Validation Depth

- Critical UI foundation with component and screenshot proof.

## Implementation Steps

1. Review existing BaseLib components and prefer enum/parameter extensions over new components.
2. Add dense/workbench variants for components that need consistent styling across pages.
3. Add strongly typed models where needed for tree/detail and metric/action toolbar composition.
4. Add sandbox examples or bUnit tests for tree/detail, dense tabs, inspector dialog, metric strip, toolbar, and compact empty/loading/error states.
5. Ensure examples use Tailwind/shared component classes only.
6. Record migration guidance for downstream page subbundles.

## Scope Exceptions

- Do not migrate every page in this subbundle.
- Do not replace existing canvas components.
- Do not tune mobile/medium layouts beyond avoiding obvious shared-component breakage.

## Do Not Do

- Do not add page-local CSS.
- Do not create abstractions with only one trivial use unless the component is needed to stop repeated page-local styling.
- Do not hard-code project/process/workflow domain strings into BaseLib components.

## Acceptance Checklist

- Tree/detail workspace pattern is reusable.
- Dense tab workspace supports large desktop tab bodies.
- Inspector dialog pattern supports context rail, main content, review panel, validation, and footer.
- Metric/action toolbar patterns are compact and reusable.
- Examples/tests exist and downstream subbundles can reference them.

## Proof Required

- Component tests or sandbox screenshots for new/extended primitives.
- Diff review proving no page-local CSS.
- Updated component candidate inventory if implementation changes the component names.

## Browser Validation Logging

- Routes: component sandbox routes if available, then representative product routes after page wiring.
- Viewport: large desktop, recommended `1920x1080`.
- Actions: render tree/detail, switch dense tabs, open inspector dialog, use toolbar overflow, show compact empty/loading/error states.
- Screenshots: each primitive in default and dense/workbench state.
- Review questions: do components solve page needs without custom CSS, do they preserve function slots, and do labels fit.

## Progression Gate

- Page-level tree, tab, and dialog redesigns may start only after these reusable patterns are implemented or an explicit exception is documented.

## Suggested Agent Prompt

```text
Implement subbundle 00-03 only. Extend BaseLib components for reusable tree/detail workspaces, dense tab bodies, inspector dialogs, compact metric strips, entity action toolbars, and compact states. Keep changes generic, use Tailwind/shared component mechanisms only, add examples/tests, and stop before page-specific wiring.
```
