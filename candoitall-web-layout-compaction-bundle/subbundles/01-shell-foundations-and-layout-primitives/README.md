# shell-foundations-and-layout-primitives

## Status

- `Completed`

## Objective

- Reclaim large-screen width and first-screen height through shared shell, scaffold, header, toolbar, help-popover, dialog, and form-control improvements so downstream routes can become denser without route-specific hacks.

## Covered Inputs

- `ART-01` large-screen-first optimization
- `ART-02` projects screenshot as the motivating example for shared density work
- Request note about moving secondary text behind `?`
- Request note about tuning component flexibility
- Request note about preferring Tailwind and proving watch behavior

## Prerequisites

- `none`

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components\Components\AppShell.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\Layout\MainLayout.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Layout\PageScaffold.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\PageHeader.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\FilterBar.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Toolbar.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\ToolbarRow.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\ToolbarFields.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\TextBox.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\DropDown.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Modals\Dialog.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\HelpPopover.razor`
- `C:\repositories\CanDoItAll\Tailwind\input.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\workbench-shell.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\page-header.css`
- `C:\repositories\CanDoItAll\Tailwind\forms\fields.css`

## Deliverables

- Wider shell and scaffold limits for standard large-screen pages.
- A reusable compact-header pattern that can keep secondary explanatory copy behind a small help affordance.
- Shared filter and toolbar layout behavior that supports one-line large-screen search plus filters plus reset rows.
- Shared modal shell density improvements.
- Proven Tailwind watch behavior for imported modules under `Tailwind/input.css`.

## Dependency Impact

- Subbundles `02`, `03`, and `04` depend on this work.
- If this phase is weak, downstream routes will either stay wasteful or accumulate one-off layout hacks that become maintenance debt immediately.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Increase the effective large-screen width budget in the shell and standard page scaffold without breaking focus-workbench surfaces.
2. Add or expose compact header composition that lets callers keep critical copy visible and move secondary guidance behind `HelpPopover`.
3. Tune shared toolbar and filter-bar composition so dense routes can keep search, filters, and reset inline on wide screens.
4. Verify shared input and select components still stretch naturally and can be width-limited by callers when needed.
5. Tighten shared dialog padding and footprint so operational modals waste less space.
6. Touch imported Tailwind modules and verify the watch process rebuilds `output.css`.

## Scope Exceptions

- Do not redesign the sidebar navigation taxonomy or shell information architecture.

## Do Not Do

- Do not create a second layout system just for this initiative.
- Do not add new standalone CSS files when the change belongs in existing Tailwind imports.
- Do not change route-level data or service behavior in this phase.

## Acceptance Checklist

- The shell and page scaffold use more width on a large desktop viewport.
- Shared headers support a denser composition without losing access to secondary guidance.
- Shared filter and toolbar composition can keep search, selects, and reset inline on large screens.
- Shared dialog shells gain more useful body area without clipping content.
- Tailwind watch is proven from an imported file change, not only from a direct inline file edit.

## Proof Required

- Tailwind watch log update in `C:\repositories\CanDoItAll\output\tailwind\watch.stdout.log`
- Updated `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\wwwroot\css\output.css`
- Browser proof on `/projects` and `/settings`
- Large-screen screenshot plus narrower-width follow-up

## Browser Validation Logging

- Target routes: `/projects`, `/settings`
- Viewports: `1720x1160`, `1280x900`
- Required browser actions: open route, resize, close startup modal when necessary, inspect header and toolbar layout, capture screenshot
- Required screenshot paths:
  - `output/playwright/subbundle-01-projects-large.png`
  - `output/playwright/subbundle-01-settings-large.png`
- Required review answers:
  - does the shell still feel coherent?
  - is the page using noticeably more width?
  - are header and toolbar regions shorter?

## Progression Gate

- Downstream work may proceed only after the shared shell, header, toolbar, dialog, and Tailwind-watch proof are all recorded and no obvious large-screen clipping or spacing regressions remain on `/projects` and `/settings`.

## Suggested Agent Prompt

```text
Implement only subbundle 01.
Treat shell width, page scaffold width, shared compact header/help behavior, filter-row composition, dialog density, and Tailwind-watch verification as one coherent foundation.
Do not start route-specific cleanup until the shared behavior is browser-proven.
```
