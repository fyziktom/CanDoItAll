# architecture-contract-and-scope-model

## Status

- `Ready`

## Objective

- Define the semantic theme contract, scope boundaries, canonical prefix direction, and public-API rules for the new non-canvas theme system.

## Covered Inputs

- `N01`, `N02`, `N03`, `N04`, `N05`, `N06`, `N07`, `N08`, `N09`
- `R01`, `R02`, `R03`, `R07`, `R08`, `R09`, `R10`

## Prerequisites

- none

## Exact Source References

- `C:\repositories\CanDoItAll\Tailwind\input.css`
- `C:\repositories\CanDoItAll\Tailwind\controls\buttons.css`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\Button.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Buttons\ButtonPrimitives.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Feedback\AlertPrimitives.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.BaseLib\Components\Badges\BadgePrimitives.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Core\CanvasThemeTokenPack.cs`
- `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\analysis\01-current-state.md`
- `C:\repositories\CanDoItAll\solution-theme-system-bundle-v1\inventories\01-scope-inventory.md`

## Deliverables

- Written architecture contract in `architecture/01-target-solution.md`
- Explicit scope boundaries and exclusions
- Public-API position on descriptive enums versus shorthand tone strings
- Canonical `cad-*` prefix decision plus compatibility direction

## Dependency Impact

- Every later subbundle depends on this phase because the token vocabulary, prefix strategy, and override model decide what code should be changed and what compatibility behavior must be preserved. Weak proof here would make later screenshots untrustworthy.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Inspect the current Tailwind entry, BaseLib primitive tone enums, and existing shared selectors.
2. Decide the override contract for NuGet consumers.
3. Decide the canonical non-canvas prefix and the compatibility strategy.
4. Document rejected alternatives such as shorthand string tones.
5. Update the architecture, inventories, and traceability files so downstream subbundles have a stable contract.

## Scope Exceptions

- Canvas-only selector cleanup is not closed in this phase.
- Zyphonote app refactors are not implemented in this phase.

## Do Not Do

- Do not edit product code yet.
- Do not silently narrow the user’s override requirement into “can fork BaseLib CSS.”
- Do not create string-based tone APIs.

## Acceptance Checklist

- Architecture names semantic tokens instead of raw palette values.
- Architecture explains how NuGet consumers override the theme without rebuilding BaseLib Tailwind.
- Architecture explicitly sets `cad-*` as canonical for shared non-canvas surfaces.
- Scope boundaries and exclusions are explicit.

## Proof Required

- Updated architecture and inventory documents
- No browser proof required for closure

## Browser Validation Logging

- `N/A`

## Progression Gate

- The architecture document, inventory summaries, and traceability table must all agree on the semantic token contract, override mechanism, prefix direction, and excluded scope.

## Suggested Agent Prompt

```text
Implement this subbundle only. Do not edit product code. Produce the architecture contract, scope boundaries, and exact public-API position for the shared non-canvas theme system.
```
