# Implementation Prompt — PromptFactorySessionGraphAdapter

Implement `PromptFactorySessionGraphAdapter` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Prompt Factory domain components`
- Status today: `partial`
- Priority: `P0`
- Scope: `domain-specific`
- JS bridge: `none`

## Objective

Project Prompt Factory editor/session state into shared graph nodes, links, groups, and selection metadata.

## Required behavior

- Build the session graph, selection graph, branch nodes, and run-node projections.
- Attach node kinds, labels, chips, and contextual actions.
- Persist and rehydrate canvas UI state coherently with the domain model.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.razor#L1-L2866`
- `src/CanDoItAll.Modules.Factory/Pages/PromptFactoryPage.Catalog.cs#L1-L1090`
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`

## Recommended implementation locations

- `src/CanDoItAll.Modules.Factory/CanvasAdapters/PromptFactorySessionGraphAdapter.cs`
- `tests/CanDoItAll.Tests.Components/PromptFactorySessionGraphAdapterTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `PromptFactorySessionGraphAdapter` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `PromptFactorySessionGraphAdapter` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
