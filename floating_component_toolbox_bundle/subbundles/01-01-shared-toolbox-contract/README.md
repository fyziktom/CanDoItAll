# 01 Shared Toolbox Contract

## Status

- Status: `Ready`

## Objective

- Add a reusable OverlayLib toolbox model and component that can render catalog sections, groups, items, search, count/status chips, empty state, and item callbacks for canvas-like hosts.

## Covered Inputs

- R1: generic floating component toolbox across canvas types.
- R2: implement generic way in the proper library when feasible.

## Prerequisites

- Existing `OverlayWindow` behavior remains trusted.
- Existing `CanvasFloatingWindow` adapter remains available for CanvasLib hosts.

## Exact Source References

- C:\repositories\CanDoItAll\src\CanDoItAll.Components.OverlayLib\Components\Core\OverlayWindow.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.OverlayLib\Models\OverlayWindowState.cs
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Components\Core\CanvasFloatingWindow.razor
- C:\repositories\CanDoItAll\src\CanDoItAll.Components.OverlayLib\CanDoItAll.Components.OverlayLib.csproj

## Deliverables

- New OverlayLib toolbox view models.
- New OverlayLib toolbox body component.
- Shared CSS for toolbox body layout that can live inside `OverlayWindow` or `CanvasFloatingWindow`.
- Component-level test coverage for render and callback behavior where practical.

## Dependency Impact

- Canvas host migration depends on the generic item/event contract.
- WebGL toolbox authoring depends on the same component being usable outside CanvasLib.

## Validation Depth

- Build `CanDoItAll.Components.OverlayLib`.
- Add targeted component tests for grouped items, empty state, primary action, and optional secondary action if a component test project already has suitable infrastructure.

## Implementation Steps

- Create presentation models under OverlayLib.
- Create `OverlayComponentToolbox.razor` and CSS.
- Keep search text host-owned through `SearchText` and `SearchTextChanged`.
- Emit item action IDs through EventCallback instead of owning domain behavior.
- Provide stable data-testid defaults and allow host overrides.

## Do Not Do

- Do not reference project/process/prompt domain models from OverlayLib.
- Do not move create or persistence logic into OverlayLib.
- Do not replace the CanvasLib runtime right-click context menu in this phase.

## Acceptance Checklist

- Generic toolbox renders sections, groups, items, counts, search, and empty state.
- Generic toolbox works inside existing floating window shells.
- Generic toolbox primary item callback returns the expected action ID.
- Generic toolbox does not depend on CanvasLib.

## Proof Required

- OverlayLib build output.
- Targeted component test output or documented reason if no matching harness exists.
- Source diff showing no domain dependencies introduced into OverlayLib.

## Browser Validation Logging

- N/A for this foundation unless a sandbox route is updated in the same subbundle.

## Progression Gate

- Downstream host migration may start only after the shared component builds and callback semantics are proven.

## Suggested Agent Prompt

- Implement the OverlayLib generic toolbox contract. Keep it presentation-only, preserve `OverlayWindow`, add tests for render/callback behavior, and stop before migrating host pages.
