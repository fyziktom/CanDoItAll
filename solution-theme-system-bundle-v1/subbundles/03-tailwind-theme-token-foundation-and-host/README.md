# tailwind-theme-token-foundation-and-host

## Status

- `Ready`

## Objective

- Add the shared semantic theme-variable foundation to the Tailwind-owned stylesheet and introduce the minimal BaseLib runtime host needed to scope light and dark themes.

## Covered Inputs

- `N01`, `N02`, `N03`, `N05`, `N10`
- `R01`, `R02`, `R03`, `R04`

## Prerequisites

- Subbundles `01` and `02` completed and trusted

## Exact Source References

- `C:\repositories\CanDoItAll\Tailwind\input.css`
- `C:\repositories\CanDoItAll\Tailwind\controls\buttons.css`
- `C:\repositories\CanDoItAll\Tailwind\surfaces\cards.css`
- `C:\repositories\CanDoItAll\Tailwind\forms\fields.css`
- `C:\repositories\CanDoItAll\Tailwind\navigation\page-header.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\CanDoItAll.Components.BaseLib.csproj`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Components\App.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.Sandbox\Components\App.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Core\CanvasThemeTokenPack.cs`

## Deliverables

- Shared semantic theme variables for light and dark themes
- Tailwind component-layer access to those semantic variables
- Minimal BaseLib theme host/scope and built-in theme keys
- One rendered route capable of switching themes at runtime

## Dependency Impact

- Every later UI phase depends on this foundation. If the token layer or runtime host is wrong, later primitive adoption and route screenshots only prove local overrides, not a working shared theme system.

## Validation Depth

- `Critical UI foundation`

## Implementation Steps

1. Add a dedicated Tailwind foundation file for the semantic non-canvas theme contract.
2. Wire that file into the Tailwind entry in the correct order.
3. Introduce the minimal BaseLib host/scope needed to apply a named theme at runtime.
4. Add built-in `light` and `dark` theme definitions.
5. Update one visible route or host surface so the theme can be switched during runtime proof.

## Scope Exceptions

- This phase does not migrate every component or page off hard-coded colors yet.

## Do Not Do

- Do not spread theme toggling logic across unrelated pages.
- Do not force downstream apps to import BaseLib’s Tailwind source files.
- Do not rename every shared selector in this phase.

## Acceptance Checklist

- The compiled BaseLib stylesheet contains semantic theme variables and built-in light/dark scopes.
- A runtime host exists and can switch the active theme without reloading the entire app.
- The foundation does not break stylesheet packaging for Web or Sandbox hosts.

## Proof Required

- `npm run build` from `C:\repositories\CanDoItAll\Tailwind`
- A rendered route showing the same surface in light and dark modes during the same session
- Screenshots at desktop and narrow widths

## Browser Validation Logging

- Target route: `/groups/foundations` or the equivalent proof route introduced for the host
- Viewports: `1600x1000` and one narrow/mobile pass
- Required actions: open the route, toggle from light to dark on the same surface, assert the theme attribute/scope changed, capture screenshots for both states
- Evidence paths: `evidence/theme-foundations-desktop.png`, `evidence/theme-foundations-mobile.png`, `evidence/theme-runtime-light.png`, `evidence/theme-runtime-dark.png`
- Review questions: Is contrast still readable, do cards and controls keep their intended hierarchy, and do radii remain coherent after theme switching?

## Progression Gate

- Tailwind build must pass, the runtime host must switch themes on a real route, and the screenshots must show materially different but still coherent light/dark rendering on the same UI.

## Suggested Agent Prompt

```text
Implement this subbundle only. Add the semantic theme foundation and the minimal runtime theme host. Keep the contract overridable by downstream apps without requiring them to rebuild BaseLib Tailwind.
```
