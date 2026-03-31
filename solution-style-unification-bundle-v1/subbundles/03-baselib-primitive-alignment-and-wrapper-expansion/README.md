# BaseLib primitive alignment and wrapper expansion

## Status

- `Blocked`

## Objective

- Align BaseLib primitives with the shared Tailwind style system, add missing reusable primitives only when genuinely needed, and prove the updated primitives on dependent routes before wide page migration begins.

## Covered Inputs

- `REQ-02`, `REQ-10`, `REQ-11`, `REQ-17`, `REQ-18`
- Raw prompt step `4`
- Raw prompt rule about creating missing useful BaseLib components when needed

## Prerequisites

- Subbundle `01` completed.
- Subbundle `02` completed with passing build proof and shell/browser smoke.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\Button.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Forms\FormField.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Cards\Card.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Navigation\PageHeader.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Typography\TextBlock.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\StyledComponentBase.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Projects\Pages\ProjectsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Badges\Pill.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Badges\PillList.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\Button.razor`

## Deliverables

- Updated BaseLib primitives that consume the shared Tailwind class families instead of ad hoc per-component combinations where reasonable.
- New reusable BaseLib primitives only for repeated patterns that cannot be expressed cleanly with the current library.
- Compatibility wrappers kept or expanded where they reduce migration churn without hiding semantics.
- Wrapper expansion for repeated component-level patterns such as stat pills, metric cards, and prefixed fields when the refreshed census proves they have multi-surface reuse.

## Dependency Impact

- Subbundle `04` depends on these primitives being stable so page migrations can remove repeated raw markup instead of re-creating it.
- If BaseLib changes are visually wrong or semantically incomplete here, downstream page proof is not trustworthy.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Map the highest-frequency repeated families onto existing BaseLib primitives where possible.
2. Add or expand primitives only when the current library cannot express a repeated pattern cleanly.
3. Update compatibility wrappers if they help preserve call sites during migration.
4. Rebuild Tailwind and the solution.
5. Browser-validate at least one dependent route that uses the updated primitives heavily.

## Scope Exceptions

- Page-level migration of app and module markup is deferred to subbundle `04`.

## Do Not Do

- Do not bulk-migrate every page in this phase.
- Do not invent trivial wrapper components with one call site and no reuse.
- Do not change CanvasLib or canvas-host surfaces.

## Acceptance Checklist

- BaseLib primitives for the top repeated families consume the shared styling system.
- Any new primitive has a clear multi-surface reuse case.
- Dependent pages can remove repeated raw styling because the component surface is now sufficient.
- Browser proof shows no regression on at least one dependent route.

## Proof Required

- `npm run build` from `C:\repositories\CanDoItAll\Tailwind`
- `dotnet build C:\repositories\CanDoItAll\CanDoItAll.slnx`
- Playwright screenshots for `/projects` and one additional dependent route such as `/resources` or `/settings`
- Execution-report gate row updated with the dependent-route smoke result

## Browser Validation Logging

- Target routes: `/projects` plus one dependent route that uses the updated primitives
- Required viewports: `1600x960` and `1280x900`
- Required Playwright actions: navigate, open the route, interact with updated controls where applicable, snapshot, and screenshot
- Required screenshot findings: no broken button density, no field-label regressions, no card/header spacing regressions, and no clipped overlays if affected

## Progression Gate

- Tailwind build and solution build both pass.
- One dependent route beyond the immediate primitive surface is browser-validated successfully.
- The execution report records the dependent-route smoke and screenshot paths.

## Suggested Agent Prompt

```text
Align BaseLib primitives to the shared Tailwind style system, add missing reusable primitives only where the census proves they are needed, and prove the result on dependent routes before allowing wide page migration.
```
