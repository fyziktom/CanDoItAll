# Shared Tabs Foundation And Cad Style Unification

## Status

- `Completed`

## Objective

- Refactor the shared BaseLib `Tabs` component so its canonical style and customization contract are owned by the CanDoItAll CAD/CDA Tailwind system instead of the current mixed `cad` and `zy` selector split.

## Covered Inputs

- `N001` current tabs styles are not working correctly
- `N002` unify away from `zy` and `cad` split
- `N003` add parameter-driven appearance customization and root `Class` extension
- `N004` use Radzen only as reference, not as shipped styling
- `N008` provide an optional light border treatment

## Prerequisites

- Bundle readiness gate passed
- `none` beyond bundle readiness because this is the first critical foundation phase

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\Tabs.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TabsItem.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\TabsPrimitives.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\StyledComponentBase.cs`
- `C:\repositories\CanDoItAll\Tailwind\navigation\tabs.css`
- `C:\repositories\CanDoItAll\Tailwind\foundation\theme.css`
- `C:\repositories\CanDoItAll\Tailwind\input.css`
- `C:\repositories\radzen-blazor\Radzen.Blazor\themes\components\blazor\_tabs.scss`

## Deliverables

- A shared tabs class and styling contract that no longer depends on shipped `zy-*` selectors
- Tailwind-owned tabs styling aligned to the existing `cad` token system
- Root `Class` support and enum-backed appearance parameters for look tuning
- An optional light border treatment
- Focused component-test coverage for the shared tabs component

## Dependency Impact

- Subbundle 02 depends on this phase for every dedicated sandbox example. If the shared contract is weak, the example lab can only hide defects, not reveal them.
- Subbundle 03 depends on this phase because browser screenshots and raw-note closure are invalid if the underlying component contract still mixes styling systems or lacks the requested customization hooks.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Inspect the current `Tabs` markup, the scoped `Tabs.razor.css`, and the Tailwind navigation stylesheet to identify which selectors and tokens must become canonical.
2. Choose the final shared class contract within the existing CAD/CDA family and remove the shared `zy-*` dependency from emitted markup and maintained styles.
3. Add the requested appearance customization surface, including root `Class` support and an enum-backed optional border path.
4. Rebuild Tailwind so the generated BaseLib stylesheet reflects the new source-of-truth.
5. Add or update focused component tests for emitted classes, missing-title fallback, and appearance-parameter behavior.
6. Perform a baseline browser pass on the existing navigation route before downstream sandbox work begins.

## Scope Exceptions

- Reorder, prevent-change, or other Radzen-specific interactive features are not in scope unless the current repo already exposes a bug that makes them relevant to the requested repair.

## Do Not Do

- Do not copy Radzen Sass, classes, or JavaScript.
- Do not hand-edit `output.css`.
- Do not leave the component emitting shared `zy-*` selectors after claiming unification.
- Do not introduce page-local CSS workarounds in place of component parameters.

## Acceptance Checklist

- The rendered shared `Tabs` component no longer depends on shared `zy-*` selectors.
- Styling lives in Tailwind-owned sources and rebuilds cleanly into the generated stylesheet.
- The component exposes root `Class` support and at least one enum-backed appearance path.
- The optional light border treatment exists and is not forced on every usage.
- Existing keyboard, disabled, icon, badge, position, and render-mode behavior still works.
- Focused component tests cover the new contract.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter Tabs`
- Tailwind rebuild command from `C:\repositories\CanDoItAll\Tailwind`
- Headed browser pass on the current sandbox navigation route with desktop screenshot review
- Source inspection confirming the shared component no longer relies on `zy-*` classes

## Browser Validation Logging

- Target route: `/groups/navigation?scenario=happy-path`
- Viewports:
- `1600x900` desktop first pass
- `900x1024` narrower follow-up if layout changed materially
- Required Playwright actions:
- open the route
- click at least two tabs
- verify active-state movement and panel-content switch
- capture a screenshot after the intended styling loads
- Screenshot artifact names:
- `output/playwright/baselib-tabs-repair-2026-04-09/01-foundation-desktop.png`
- `output/playwright/baselib-tabs-repair-2026-04-09/01-foundation-narrow.png`
- Required screenshot review answers:
- readable labels
- obvious active state
- no overlap or clipped panel chrome
- optional border treatment feels intentional, not noisy

## Progression Gate

- Downstream work may continue only after the shared component proves the new styling contract, the component tests pass, the desktop browser screenshot is acceptable, and no shared `zy-*` dependency remains in emitted markup or maintained styles.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Refactor the shared BaseLib Tabs component onto the CAD/CDA Tailwind contract, add parameter-driven appearance customization including root Class support and an optional border treatment, preserve accessibility and existing behavior, rebuild Tailwind, and prove the result with focused component tests plus a baseline browser pass on the current sandbox navigation route.
```
