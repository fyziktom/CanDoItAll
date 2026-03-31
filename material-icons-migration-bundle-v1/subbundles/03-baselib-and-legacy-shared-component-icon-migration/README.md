# BaseLib And Legacy Shared-Component Icon Migration

## Status

- `Ready`

## Objective

- Migrate the reusable BaseLib and legacy shared-component surfaces that still render Font Awesome markup or raw icon text so downstream pages inherit the new Material icon behavior.

## Covered Inputs

- `N004` Map all places where `Icon.razor` or pure icons are used and replace them.
- `N006` Keep the shared foundation centered in BaseLib while aligning the legacy shared component project that the web app still references.

## Prerequisites

- `subbundles/01-icon-census-tracker-workbook-and-migration-map` completed and trusted.
- `subbundles/02-local-material-icons-foundation-and-shared-renderer-conversion` completed and trusted.

## Exact Source References

- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Buttons/Button.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Navigation/Steps.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Navigation/Tabs.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Navigation/TreeViewNodeRow.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Forms/TagEditor.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/Button.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/Steps.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/Tabs.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/AppShell.razor`
- `C:/repositories/CanDoItAll/src/CanDoItAll.Components/Components/AppTabStrip.razor`

## Deliverables

- Shared BaseLib and legacy shared-component renderers updated to Material icon output.
- Raw glyph escapes in shared shell or shared primitives replaced with Material icon rendering or mapped tokens.
- Shared CSS hooks aligned to the new icon class contract.

## Dependency Impact

- Shell, tabs, steps, buttons, and tree rows are reused broadly, so downstream route proof depends on these components being correct first.
- Weak proof here would invalidate later app and Workbench screenshots because those routes inherit these components.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Replace direct Font Awesome rendering in shared `Button`, `Steps`, and `Tabs`.
2. Update treeview, shared shell, tab-strip actions, and tag editor raw glyph surfaces to Material icon rendering.
3. Update shared CSS selectors and layout hooks that still target Font Awesome or raw glyph wrappers.
4. Re-test the shared component surfaces before later route work begins.

## Scope Exceptions

- If a legacy shared component is still required by the web app, it is in scope here even if BaseLib already has the preferred implementation.

## Do Not Do

- Do not leave mixed Font Awesome and Material icon output inside the shared component layer.
- Do not hardcode route-specific icon fixes when the shared component can own the change.
- Do not overwrite local edits in `TreeViewNodeRow.razor`; merge with them carefully.

## Acceptance Checklist

- Shared components no longer emit Font Awesome markup.
- Shared raw glyph surfaces are replaced with Material icon output or mapped tokens.
- Shared CSS no longer depends on `.rz-fa-icon` for these components.
- Workbook rows for the touched shared files are updated.

## Proof Required

- `dotnet build C:/repositories/CanDoItAll/CanDoItAll.slnx`
- Browser proof on `/`, `/groups/navigation`, `/groups/foundations`, and `/projects`
- Desktop and narrower-width screenshots showing shared icon alignment in buttons, tabs, tree rows, and shell actions
- One dependent-route smoke proving downstream consumers still render correctly

## Browser Validation Logging

- Route: `/`, `/groups/navigation`, `/groups/foundations`, `/projects`
- Viewports: `1600x900` first pass, then `768x1024`
- Actions: navigate, exercise shared shell and navigation controls, and capture screenshots after representative shared icons render
- Screenshots: record the actual file paths in `reviews/01-execution-report.md`
- Review questions: confirm tabs, buttons, tree rows, and shell actions still have clear affordances and correct spacing

## Progression Gate

- Do not start subbundle `04` until shared-shell and shared-navigation proof is trusted and no touched shared component still relies on Font Awesome output.

## Suggested Agent Prompt

```text
Implement only subbundle 03. Migrate the reusable BaseLib and legacy shared components to the new Material icon contract, update their CSS hooks, and prove the shared shell and navigation routes before moving on.
```
