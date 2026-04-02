# workbench-and-prompt-factory-overlays

## Status

- `Completed`

## Objective

- Compact the custom prompt-factory dialogs and workbench overlay surfaces so modal and overlay efficiency matches the cleaned-up main pages, with explicit open-state proof for clipping, layering, and action visibility.

## Covered Inputs

- Request note about analyzing modals, not only pages
- Request note about helper affordances
- Request note about component flexibility when surfaces do not behave like a UI developer expects

## Prerequisites

- `subbundles/01-shell-foundations-and-layout-primitives`
- `subbundles/02-projects-page-and-project-modals`
- `subbundles/03-list-detail-pages-and-settings-density`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\PromptFactoryPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Factory\Pages\Components\PromptFactoryDialogs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectCalendarPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureOverlayDialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureCanvasDialogs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\Components\ProjectStructure\ProjectStructureSupportDialogs.razor`

## Deliverables

- Prompt factory dialogs that consume less space and align with the shared modal density direction.
- Project structure and calendar support chrome that uses the available large-screen space intentionally.
- Workbench overlay dialogs that stay readable and unclipped in open state.

## Dependency Impact

- This subbundle closes the modal and overlay half of the initiative.
- Weak proof here would leave the app inconsistent: compact page layouts outside the workbench, but oversized or clipped overlays inside it.

## Validation Depth

- `UI, overlay-open-state, and browser-proof`

## Implementation Steps

1. Tighten prompt factory dialog shells, headers, and action rows without hurting editing affordances.
2. Compact project calendar and project structure page-level support chrome where it is wasting height or width.
3. Rework project structure overlay dialog shells and related dialog content so open-state actions remain visible and readable.
4. Introduce help affordances where secondary explanatory copy is crowding high-value overlay space.

## Scope Exceptions

- Do not change canvas behavior, graph data, or prompt-factory workflow logic unless a minimal UI fix truly depends on it.

## Do Not Do

- Do not validate these overlays only in their closed state.
- Do not swap custom workbench overlays to the shared dialog shell blindly if that breaks canvas-scoped positioning.
- Do not shrink text until it becomes less readable just to gain density.

## Acceptance Checklist

- Prompt factory dialogs feel denser but still support editing and confirmation comfortably.
- Project structure overlay dialogs keep actions visible and content unclipped when open.
- Workbench page-level support chrome reaches the core canvas or calendar surface faster on desktop.
- Overlay help and action affordances remain discoverable.

## Proof Required

- Browser proof on `/prompt-factory`
- Browser proof on at least one workbench route with open overlays:
  - `/projects/{id}/structure`
  - `/projects/{id}/calendar`
- Open-state screenshots for prompt factory dialogs and project structure overlays

## Browser Validation Logging

- Target routes: `/prompt-factory`, `/projects/{id}/structure`, `/projects/{id}/calendar`
- Viewports: `1720x1160`, `1280x900`
- Required browser actions:
  - open the prompt preview and component editor dialogs
  - open project structure overlay dialogs and support dialogs
  - inspect overlay geometry and action visibility
- Required screenshot paths:
  - `output/playwright/subbundle-04-prompt-factory-dialog-large.png`
  - `output/playwright/subbundle-04-structure-overlay-large.png`
  - `output/playwright/subbundle-04-calendar-large.png`
- Required review answers:
  - is the overlay content fully visible?
  - are actions reachable without awkward scrolling?
  - did compaction preserve clarity on canvas-heavy surfaces?

## Progression Gate

- Final closure work may proceed only after prompt factory and workbench overlays have open-state proof showing correct layering, no clipping, and materially better use of space.

## Suggested Agent Prompt

```text
Implement only subbundle 04.
Treat prompt factory dialogs and workbench overlays as first-class UI surfaces, not as exceptions that can skip density work.
Preserve behavior, but tighten the shells and validate them open.
```
