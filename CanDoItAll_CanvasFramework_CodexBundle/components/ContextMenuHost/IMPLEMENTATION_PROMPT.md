# Implementation Prompt — ContextMenuHost

Implement `ContextMenuHost` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Overlay, inspector, and helper components`
- Status today: `partial`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `required`

## Objective

Own context-menu placement, nested menus, keyboard dismissal, focus handling, and action dispatch.

## Required behavior

- Project Structure node actions, create menus, and utility commands.
- Prompt Factory node and session context actions.
- Future clipboard, validation, and diagnostics menu groups.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/wwwroot/js/canvasWorkbenchInterop.js#L1-L4099`
- `src/CanDoItAll.ComponentKit/wwwroot/canvas-workbench.css#L1-L2309`
- `src/CanDoItAll.Modules.Factory/PromptFactoryCanvasCatalog.cs#L1-L645`
- `src/CanDoItAll.Modules.Workbench/ProjectStructureCanvasCatalog.cs#L1-L326`
- `docs/ui-shared-components/recommendations/missing-components.md#L1-L241`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Graph/ContextMenuHost.cs`
- `src/CanDoItAll.ComponentKit/Components/ContextMenuHost.razor`
- `src/CanDoItAll.ComponentKit/wwwroot/js/context-menu-host.js`
- `tests/CanDoItAll.Tests.Components/ContextMenuHostTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `ContextMenuHost` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `ContextMenuHost` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
