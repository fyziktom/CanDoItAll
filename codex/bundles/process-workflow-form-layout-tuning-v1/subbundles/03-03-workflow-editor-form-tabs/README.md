# 03-workflow-editor-form-tabs

## Status

- `Completed`

## Objective

- Reorganize the Workflows page Editor inspector so definition, selected-node setup, route, and preview/validation forms are separated into tabs.

## Success Criteria

- `WorkflowCanvasEditor.razor` no longer stacks all inspector forms together.
- Definition, node setup, routes, and preview/validation controls remain reachable.
- Existing canvas, toolbox, component library, node, executor, edge, validation, preview, and save behavior remains intact.
- Styling remains aligned with existing component CSS and no new visual theme is introduced.

## Covered Inputs

- `N004`
- `N005`

## Prerequisites

- `subbundles/01-01-layout-inventory-and-proposals` completed.
- Prepared-stage bundle validator passed.

## Exact Source References

- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/WorkflowsPage.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor`
- `repo://src/CanDoItAll.Modules.AgentFramework/Pages/Components/WorkflowCanvasEditor.razor.css`

## Deliverables

- Inspector tabs in `WorkflowCanvasEditor.razor`.
- Existing selected-node and executor setup forms moved into the Node setup tab.
- Existing edge editor moved into a Routes tab.
- Existing validation and preview input controls moved into a Preview or Validation tab.

## Dependency Impact

- Final browser proof and raw-note closure depend on this phase.
- Weak proof here would invalidate claims that Workflows Editor received the same tuning as Processes.

## Validation Depth

- UI layout, module build, and browser-proof validation.

## Implementation Steps

1. Add component-local selected inspector tab state to `WorkflowCanvasEditor.razor`.
2. Wrap inspector content in shared `Tabs`.
3. Move definition fields to a Definition tab.
4. Move selected-node identity, node kind, shape, and instructions controls to a Node tab.
5. Keep executor selection, policy, descriptor summary, and executor-specific settings inside the Node setup tab to preserve selected-node handler scope.
6. Move edge builder and route list to a Routes tab.
7. Move validation issues, preview input JSON, and preview result to a Preview tab.
8. Preserve existing modal node details behavior.

## Scope Exceptions

- The node details modal may remain section-based because it is already separated; this subbundle focuses on the main Editor inspector.

## Do Not Do

- Do not rewrite workflow mapping, executor settings serialization, runtime services, or catalog services.
- Do not introduce a new CSS theme.
- Do not remove executor-specific settings branches.

## Acceptance Checklist

- [x] Workflow inspector uses shared tabs.
- [x] Definition, node setup, routes, and preview/validation controls remain reachable.
- [x] `CanDoItAll.Modules.AgentFramework` builds.
- [x] Browser proof captures the Editor tab at desktop and narrow widths.

## Proof Required

- Build transcript for `src/CanDoItAll.Modules.AgentFramework/CanDoItAll.Modules.AgentFramework.csproj`.
- Source assertion transcript proving inspector tabs and retained executor/route/preview sections.
- Browser screenshots for `/agents/workflows` Editor at desktop and narrow widths.

## Browser Validation Logging

- Route: `/agents/workflows`.
- Viewports: `1600x900` and `390x844`.
- Actions: open page, select Editor tab, switch inspector tabs, inspect representative controls.
- Assertions: tab labels visible, selected-node empty state or fields visible, executor controls reachable from Node setup when an executor node is selected, routes/preview panels reachable, no incoherent overlap or avoidable lateral overflow.
- Screenshots: `bundle://proof/SB04/browser/workflows-editor-desktop-definition.png`, `bundle://proof/SB04/browser/workflows-editor-desktop-node.png`, `bundle://proof/SB04/browser/workflows-editor-desktop-routes.png`, `bundle://proof/SB04/browser/workflows-editor-desktop-preview.png`, `bundle://proof/SB04/browser/workflows-editor-narrow-definition.png`.

## Progression Gate

- Final closure cannot proceed until this subbundle builds and its browser proof is recorded.

## Suggested Agent Prompt

```text
Implement this subbundle only. Keep WorkflowCanvasEditor behavior unchanged while moving inspector forms into shared tabs. Capture source and browser proof before closure.
```
