# Implementation Prompt — AnimationTimeline

Implement `AnimationTimeline` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Advanced graphical components`
- Status today: `missing`
- Priority: `P2`
- Scope: `shared`
- JS bridge: `required`

## Objective

Provide a minimal shared animation system for smooth transitions, focus pans, hover fades, and connector or badge micro-animations.

## Required behavior

- Animate fit-to-view or focus transitions.
- Fade in selection overlays or guide lines.
- Support subtle connector flow animations or badge state transitions.

## Reuse / refactor directives

- Create the component without introducing duplicate DTOs or a shadow runtime. Extend the current shared contracts and adapters rather than creating a second system.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/js/zy-canvas-calendar.js#L1-L3720`
- `docs/canvases-improvements/02-shared-canvas-system-spec.md#L1-L430`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Core/AnimationTimeline.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/animation-timeline.js`
- `tests/CanDoItAll.Tests.Components/AnimationTimelineTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `AnimationTimeline` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `AnimationTimeline` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
