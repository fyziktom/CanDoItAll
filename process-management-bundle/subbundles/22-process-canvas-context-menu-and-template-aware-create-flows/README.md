# 22 Process Canvas Context Menu And Template-Aware Create Flows

## Status

- `Completed`

## Objective

- Upgrade the authored process canvas from a rendered surface into a real workbench by adding right-click actions, grouped create flows, floating toolbox windows, and template-aware creation that reuses the extracted process forms.

## Covered Inputs

- `REQ-002`
- `REQ-006`
- `REQ-014`
- `REQ-015`
- `REQ-016`
- Canvas parity audit `CAV-02` through `CAV-05`
- User request for right-click process creation and template-aware role UX

## Prerequisites

- `20-implemented-architecture-hardening-and-form-componentization`
- `21-post-implementation-bundle-phase05-generation`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\01-workbooks\02-process-modeling-canvas-and-runtime.xlsx`
- `C:\repositories\CanDoItAll\process-management-bundle\reviews\02-implementation-coverage-audit.md`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\Components\ProcessWorkspace.razor.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Processes\ProcessCanvasSurfaceFactory.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure`

## Deliverables

- Right-click context-menu flows for process canvas background and process nodes.
- A floating process-component library/toolbox window using shared CanvasLib window patterns.
- Create flows that open floating forms instead of mutating the editor inline.
- Template-aware role creation that first asks whether the user wants to use a template or create a custom role.
- Canonical-process updates wired back into the real process editor/service instead of staying as UI-only chrome.

## Dependency Impact

- This subbundle is the foundation for the selection-inspector and edit-dialog parity in the next subbundle.
- If this work is weak, the process canvas will still behave like a diagram preview instead of a first-class authoring workbench.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Wire process canvas workbench events such as context action, create action, and related authoring callbacks.
2. Add grouped canvas creation actions for process nodes and related authoring objects.
3. Add a floating process toolbox window that mirrors the compact project-structure workbench rhythm.
4. Reuse extracted process form components so create flows do not duplicate the current inline editor markup.
5. Make role creation explicitly template-aware before falling back to custom authoring.

## Scope Exceptions

- Selection-detail windows, single-click selection sync, and double-click edit flows belong to subbundle `23`.

## Do Not Do

- Do not build a second ad hoc canvas implementation outside shared CanvasLib workbench patterns.
- Do not open raw HTML popovers when the shared floating-window system already exists.
- Do not keep the old inline-only create flow as the primary canvas authoring path.

## Acceptance Checklist

- Right-clicking canvas background opens relevant create actions.
- Right-clicking a process node opens node-relevant actions.
- The process toolbox window exists, is movable, and uses shared canvas window chrome.
- Create actions open floating process forms that reuse extracted components.
- Role creation asks for template-vs-custom choice before the role editor is committed.

## Proof Required

- Playwright walkthrough for right-click canvas creation and toolbox flows.
- Large-screen screenshots proving no overflow, clipping, or unnecessary spacing in the floating windows.
- Regression proof that created items persist through the canonical process editor/service, not only UI state.

## Browser Validation Logging

- Route:
  `/processes`
- Route:
  `/projects/{id}/processes`
- Viewport:
  `1920x1080`
- Viewport:
  `1600x900`
- Evidence:
  screenshots and Playwright steps covering right-click, toolbox, and template-aware create flows

## Progression Gate

- Subbundle `23` may not start until right-click and floating create flows are browser-validated and the forms are confirmed reusable.

## Suggested Agent Prompt

```text
Implement only the process-canvas create-flow parity slice. Wire right-click actions, grouped process create flows, and a floating toolbox window on top of shared CanvasLib workbench patterns, reuse the extracted process editor forms, and make role creation template-aware before closing.
```

