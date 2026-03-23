# Implementation Prompt — CommandHistoryStore

Implement `CommandHistoryStore` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Utility and infrastructure components`
- Status today: `partial`
- Priority: `P1`
- Scope: `shared`
- JS bridge: `none`

## Objective

Provide a shared undo/redo-ready history abstraction for editor state snapshots or domain commands.

## Required behavior

- Undo selection-safe graph edits in Prompt Factory.
- Undo node create/move/link actions in Project Structure after shared command integration.
- Feed toolbar button enabled state and keyboard shortcut routing from one consistent source.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.History.cs#L1-L234`
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Core/CommandHistoryStore.cs`
- `tests/CanDoItAll.Tests.Components/CommandHistoryStoreTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `CommandHistoryStore` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `CommandHistoryStore` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
