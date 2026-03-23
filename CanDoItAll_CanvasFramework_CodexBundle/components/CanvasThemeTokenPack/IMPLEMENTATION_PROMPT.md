# Implementation Prompt — CanvasThemeTokenPack

Implement `CanvasThemeTokenPack` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Utility and infrastructure components`
- Status today: `partial`
- Priority: `P1`
- Scope: `shared`
- JS bridge: `none`

## Objective

Centralize canvas/workbench theme tokens for color, spacing, radii, shadows, line weights, typography, and dark/light readiness.

## Required behavior

- Apply consistent card, connector, overlay, and backdrop styling across Project Structure, Prompt Factory, and Calendar.
- Enable a future dark mode without patching dozens of runtime-specific style fragments.
- Expose stable theming hooks to DALL-E prompt generation and visual QA.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `docs/ui-shared-components/recommendations/missing-components.md#L1-L241`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Core/CanvasThemeTokenPack.cs`
- `tests/CanDoItAll.Tests.Components/CanvasThemeTokenPackTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `CanvasThemeTokenPack` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `CanvasThemeTokenPack` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
