# Implementation Prompt — JsInteropBridge

Implement `JsInteropBridge` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Utility and infrastructure components`
- Status today: `partial`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `required`

## Objective

Define a stable, modular JS bridge contract for graph and calendar runtimes without leaking page-specific helpers into shared files.

## Required behavior

- Split generic scene host interop from Prompt Factory shortcut helpers and floating inspector code.
- Keep calendar lifecycle separate from graph lifecycle while sharing host conventions.
- Provide a thin boundary for future diagnostics, clipboard, and accessibility extensions.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasCalendarInterop.js#L13-L335`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.CanvasInspector.cs#L1-L37`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Core/JsInteropBridge.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/js-interop-bridge.js`
- `tests/CanDoItAll.Tests.Components/JsInteropBridgeTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `JsInteropBridge` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `JsInteropBridge` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
