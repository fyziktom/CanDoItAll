# 01 Upgrade Blocks Explorer Window Chrome And Accordion

## Status

- `Ready`

## Objective

Restore the shared floating-window chrome for the blocks explorer and make the section list behave as a reliable accordion.

## Covered Inputs

- `N001`
- `N002`
- `N003`
- `R001`
- `R002`
- `R003`
- `R006`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.ToolWindows.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\CanvasFloatingWindow.razor`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables

- visible shared floating-window header controls for the blocks explorer
- dark-mode toolbox body that fits inside the shared shell without repeated title noise
- accordion state that opens the clicked section and keeps the non-search toolbox intentional

## Implementation Steps

1. Stop opting the blocks explorer out of the shared floating-window header.
2. Retune the toolbox-specific page CSS so the restored shared shell still reads as a dark explorer.
3. Simplify the inner toolbox header so the window title is not duplicated.
4. Update the toolbox group state logic so the clicked group opens deterministically and the non-search state behaves like an accordion.
5. Add focused component coverage for the restored chrome and accordion body visibility.

## Scope Exceptions

- none

## Do Not Do

- do not reimplement minimize, hide, or drag behavior outside `CanvasFloatingWindow`
- do not turn the toolbox back into a static sidebar or inspector column
- do not weaken the dark toolbox body into a generic white card

## Acceptance Checklist

- the blocks explorer shows shared minimize, reset, and hide controls
- the window is visibly draggable through the shared drag handle
- the clicked section reveals its body content
- the non-search state does not leave every section expanded at once
- the selected source and create items still render as before

## Proof Required

- focused component coverage for visible toolbox header and accordion state
- bundle execution report updated with the exact test command and result
- browser proof can be completed in subbundle 02, but this subbundle must not regress it

## Suggested Agent Prompt

```text
Implement subbundle 01 only.

Restore the project-structure blocks explorer to the shared floating-window pattern. Reuse the standard window header, keep the toolbox body dark, and make the non-search group list behave like a real accordion without regressing create actions or selected-source context.
```
