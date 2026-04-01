# shortcut-contract-and-catalog-foundation

## Status

- `Completed`

## Objective

- Introduce a shared accelerator contract and deterministic shortcut assignment for the project-structure action tree so later runtime and help work can consume stable metadata.

## Covered Inputs

- `N002` Single-letter shortcuts should select menu items.
- `N004` Preserve requested block shortcuts.
- `N005` Preserve requested asset shortcuts.
- `N006` Preserve requested marker, meeting, people, infrastructure, note, and work shortcuts.
- `N007` Add shortcuts for other right-menu options too.
- `N009` Foundation for visible underline behavior by ensuring the chosen shortcut is explicit in action metadata.

## Prerequisites

- Prepared-stage validator passes.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Components.CanvasLib\Canvas\Workbench\CanvasWorkbenchChrome.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructureCanvasCatalog.RichDefinitions.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureCanvasCatalogTests.cs`

## Deliverables

- Shared action-model support for a menu accelerator.
- Deterministic shortcut assignment helper that preserves architect-fixed keys and avoids sibling collisions.
- Updated project-structure create and node-action menus exposing accelerators in their action metadata.
- Focused tests proving fixed mappings and collision-free sibling sets.

## Dependency Impact

- `02-runtime-keyboard-navigation-and-menu-affordances` cannot route keyboard input safely until this shortcut contract is stable.
- `03-help-modal-information-architecture-and-shortcut-docs` should not document keys until the catalog emits the final assignments.
- Weak proof here would invalidate all downstream browser evidence because the runtime would be acting on uncertain metadata.

## Validation Depth

- `Critical foundation`

## Implementation Steps

1. Extend the shared action contract to carry a single accelerator value.
2. Add or update assignment helpers in the project-structure catalog or adapter layer to apply architect-fixed keys first.
3. Fill remaining sibling shortcuts with a deterministic collision-free fallback strategy.
4. Ensure non-create node actions also receive explicit accelerators where appropriate.
5. Add or update tests that verify fixed mappings and reject duplicate sibling accelerators.

## Scope Exceptions

- Do not implement JavaScript keyboard routing in this subbundle.
- Do not update help-modal markup or styling in this subbundle.

## Do Not Do

- Do not hand-code one-off shortcut maps inside browser runtime files.
- Do not widen the catalog refactor beyond shortcut metadata assignment.
- Do not close the subbundle on inference alone; proof must show actual sibling-set uniqueness.

## Acceptance Checklist

- Shared action metadata exposes an accelerator field.
- Architect-requested mappings exist exactly where specified.
- Additional visible siblings in the same layer receive deterministic shortcuts without collisions.
- Component tests cover both explicit assignments and fallback behavior.

## Proof Required

- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProjectStructureActionCatalogAdapterTests`
- `dotnet test C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProjectStructureCanvasCatalogTests`
- Execution-report update recording which sibling sets were explicitly validated.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Downstream subbundles may continue only after automated proof confirms the architect-fixed mappings and all validated sibling layers are collision-free.

## Suggested Agent Prompt

```text
Implement only subbundle 01 for the canvas context-menu shortcuts bundle.
Add a shared accelerator contract, preserve the architect-fixed mappings, assign deterministic collision-free fallback shortcuts for the remaining sibling sets, and prove the result with focused component tests.
Do not modify JavaScript runtime routing or help-modal UI in this phase.
```
