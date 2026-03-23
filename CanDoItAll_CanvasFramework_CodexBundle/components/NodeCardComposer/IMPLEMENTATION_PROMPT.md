# Implementation Prompt — NodeCardComposer

Implement `NodeCardComposer` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Containers`
- Status today: `partial`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `required`

## Objective

Compose graph node cards from primitives: container, text, chips, icons, image slots, metadata stacks, and inline affordances.

## Required behavior

- Project Structure object cards with type, title, status, priority, and marker metadata.
- Prompt Factory session, component, input, branch, and run-step cards.
- Future reusable note, media, placeholder, or validation cards.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor#L1-L1399`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Graph/NodeCardComposer.cs`
- `src/CanDoItAll.ComponentKit/Components/NodeCardComposer.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/node-card-composer.js`
- `tests/CanDoItAll.Tests.Components/NodeCardComposerTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `NodeCardComposer` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `NodeCardComposer` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
