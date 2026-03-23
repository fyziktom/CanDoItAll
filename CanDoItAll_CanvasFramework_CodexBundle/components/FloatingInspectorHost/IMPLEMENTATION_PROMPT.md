# Implementation Prompt — FloatingInspectorHost

Implement `FloatingInspectorHost` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Overlay, inspector, and helper components`
- Status today: `partial`
- Priority: `P1`
- Scope: `shared`
- JS bridge: `required`

## Objective

Support detachable or floating inspector presentation anchored to the canvas stage.

## Required behavior

- Prompt Factory floating inspector docking and persistence.
- Future compact inspector mode for smaller screens or multi-canvas workflows.
- Contextual mini inspectors near selected nodes.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.ComponentKit/Components/CanvasWorkbenchStage.razor#L1-L82`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Graph/FloatingInspectorHost.cs`
- `src/CanDoItAll.ComponentKit/Components/FloatingInspectorHost.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/floating-inspector-host.js`
- `tests/CanDoItAll.Tests.Components/FloatingInspectorHostTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `FloatingInspectorHost` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `FloatingInspectorHost` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
