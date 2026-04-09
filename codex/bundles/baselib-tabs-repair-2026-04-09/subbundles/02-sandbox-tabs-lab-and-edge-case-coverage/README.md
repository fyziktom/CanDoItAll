# Sandbox Tabs Lab And Edge-Case Coverage

## Status

- `Completed`

## Objective

- Create a dedicated sandbox route for the shared `Tabs` component and use it to demonstrate healthy and non-optimal states that expose layout, fallback-text, wrapping, and appearance defects early.

## Covered Inputs

- `N005` add a dedicated tabs page in the components sandbox
- `N006` add long-title, missing-title, and narrow-width or wrapping edge cases
- `N007` use the examples to discover and force repair of new issues instead of hiding them
- `N008` show the optional light-border appearance in context

## Prerequisites

- `subbundles/01-shared-tabs-foundation-and-cad-style-unification` is `Completed`
- The shared tabs contract already passed its closure gate with browser proof strong enough for downstream sandbox work

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\SandboxCatalogRegistry.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\Navigation.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\Pages\NavigationTabs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TabsItem.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TabsPrimitives.cs`

## Deliverables

- A dedicated sandbox tabs route under the navigation group
- A tabs demo surface with healthy and intentionally stressed examples
- Example usage of the new appearance customization contract
- Example-driven defect discovery that reopens earlier phases when needed

## Dependency Impact

- Subbundle 03 depends on this route for final browser proof and screenshot closure.
- If this lab masks defects with page-local styling or incomplete examples, the final screenshots will not prove the shared component actually works.

## Validation Depth

- `UI, browser-proof, and responsive discovery surface`

## Implementation Steps

1. Add a dedicated tabs-focused route in the sandbox navigation group.
2. Build a compact example set that covers healthy usage plus the requested non-optimal paths.
3. Use shared BaseLib layout and page primitives for the demo surface instead of ad-hoc wrappers.
4. Exercise the new appearance parameters from subbundle 01, including the optional border treatment.
5. Run a desktop and narrower-width browser pass on the dedicated route.
6. If the new examples expose a shared-component defect, reopen subbundle 01 or this subbundle immediately and repair it before closure.

## Scope Exceptions

- The sandbox route does not need to mirror every Radzen tabs demo. It only needs enough coverage to reveal the requested CanDoItAll defects and confirm the repaired component works intentionally.

## Do Not Do

- Do not leave tabs buried only inside the mixed `/groups/navigation` page.
- Do not add page-local CSS that compensates for a shared-component bug.
- Do not skip the missing-title example because the fallback currently exists in code; it still needs visible proof.
- Do not ignore newly discovered issues just because they were not named in the original note list.

## Acceptance Checklist

- A dedicated tabs sandbox route exists and is discoverable through the navigation group.
- The route contains a healthy baseline example plus requested edge-case examples.
- The long-title and missing-title cases remain readable and intentional.
- Narrow-width or small-column behavior is visibly acceptable and documented.
- The optional border appearance is demonstrated without becoming the only style path.
- Any new defect revealed by the route is fixed or explicitly reopened before the phase closes.

## Proof Required

- Managed-app browser proof on the dedicated tabs route
- Desktop screenshot plus at least one tablet or mobile-width screenshot
- DOM or text assertions proving the expected example surfaces are present
- Execution-report note if any earlier phase was reopened due to discovered issues

## Browser Validation Logging

- Target route: `/groups/navigation/tabs`
- Viewports:
- `1600x900` desktop first pass
- `900x1024` narrower tablet-style pass
- `390x844` mobile follow-up when wrapping or stacking behavior is involved
- Required Playwright actions:
- open the dedicated route
- inspect each example section by text
- click through at least one tab group
- resize or reopen at narrower widths
- capture screenshots for desktop and smaller-width states
- Screenshot artifact names:
- `output/playwright/baselib-tabs-repair-2026-04-09/02-tabs-lab-desktop.png`
- `output/playwright/baselib-tabs-repair-2026-04-09/02-tabs-lab-tablet.png`
- `output/playwright/baselib-tabs-repair-2026-04-09/02-tabs-lab-mobile.png`
- Required screenshot review answers:
- long labels are readable
- fallback labels do not feel broken
- wraps or overflows are intentional
- no clipped tab buttons or panel content

## Progression Gate

- Subbundle 03 may start only after the dedicated tabs route exists, the requested edge cases are visible, desktop and narrower-width screenshots have been reviewed, and any example-driven defect has been fixed or explicitly reopened and reclosed.

## Suggested Agent Prompt

```text
Implement subbundle 02 only. Add a dedicated sandbox tabs route under the navigation group, populate it with healthy and edge-case examples for long titles, missing titles, narrow-width wrapping or overflow, and the optional border appearance, then prove the route in a headed browser on desktop and narrower widths. Reopen the earlier foundation phase immediately if the examples reveal a shared-component defect.
```
