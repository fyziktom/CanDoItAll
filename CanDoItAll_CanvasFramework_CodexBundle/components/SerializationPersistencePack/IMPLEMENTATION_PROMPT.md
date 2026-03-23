# Implementation Prompt — SerializationPersistencePack

Implement `SerializationPersistencePack` for the CanDoItAll shared canvas framework.

## Scope

- Component category: `Utility and infrastructure components`
- Status today: `partial`
- Priority: `P0`
- Scope: `shared`
- JS bridge: `partial`

## Objective

Normalize serializable scene state, viewport state, selection state, and UI-state persistence contracts across graph and calendar surfaces.

## Required behavior

- Persist manual positions, collapse state, selection, and viewport for workbench-like surfaces.
- Persist calendar view state in a typed model instead of string parsing.
- Support export/import-ready scene manifests and future collaboration scenarios.

## Reuse / refactor directives

- Extract the component from the listed files. Reuse current DTOs, page behavior, and JS logic where they are already correct, but move ownership into the recommended component boundary.
- Preserve working existing behavior where it already matches the target architecture.
- Move ownership into the new component boundary and remove the old ownership point when parity is reached.
- Keep business and persistence logic in C# unless the work is directly tied to rendering, hit testing, measurement, or browser APIs.

## Relevant existing files to inspect first

- `src/CanDoItAll.ComponentKit/Canvas/CanvasWorkbenchContracts.cs#L5-L340`
- `src/CanDoItAll.ComponentKit/Canvas/CanvasCalendarContracts.cs#L3-L223`
- `src/CanDoItAll.Modules.Workbench/ProjectWorkbenchModels.cs#L128-L806`
- `src/CanDoItAll.Modules.Factory/FactoryDomain.cs#L359-L536`
- `src/CanDoItAll.Modules.Factory/PromptFactoryService.cs#L238-L715`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectCalendarPage.razor#L1-L161`

## Recommended implementation locations

- `src/CanDoItAll.ComponentKit/Canvas/Core/SerializationPersistencePack.cs`
- `src/CanDoItAll.ComponentKit/wwwroot/js/serialization-persistence-pack.js`
- `tests/CanDoItAll.Tests.Components/SerializationPersistencePackTests.cs`

## Constraints

- Do not create a second parallel abstraction next to `SerializationPersistencePack` once the dedicated boundary exists.
- Do not move domain business rules into JS unless the work is truly rendering-, pointer-, or browser-API-centric.
- Do not leave the page-level fallback implementation in place after migrating the shared/domain-specific component.

## Acceptance criteria

- `SerializationPersistencePack` exists as a first-class boundary in the recommended location.
- The implementation reuses existing shared DTOs/contracts where appropriate instead of creating duplicates.
- The implementation integrates through the shared/domain adapter seams described in this bundle.
- Tests are added or updated at the correct level.
- The old leakage point or duplicate path is removed or explicitly marked as transitional.

## Done means

The component is integrated, tested, and the former ownership leak is no longer the strategic implementation path.
