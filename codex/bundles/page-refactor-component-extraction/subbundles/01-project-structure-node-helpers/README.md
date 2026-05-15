# Project Structure Node Helpers

## Status

- `Completed`

## Objective

- Extract pure ProjectStructure node helper logic from `ProjectStructurePage.razor` into a strongly typed `ProjectStructureNodeHelpers` class without changing canvas, attachment, selection, or command behavior.

## Covered Inputs

- `N003`
- `N004`
- `R002`

## Prerequisites

- Prepared-stage bundle validator passed.
- Workbook rows for ProjectStructure helper candidates are populated.

## Exact Source References

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCanvasCatalog.cs`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`

## Deliverables

- `ProjectStructureNodeHelpers` created near the Workbench project-structure code.
- Static node helpers for labels, priority, markers, attachment preview classification, attachment display fields, outline weight, and simple note title are moved out of the page where dependencies are pure.
- Existing page methods that depend on component state remain on the page.

## Dependency Impact

- `02-project-structure-page-shell-components` depends on this phase because shell extraction must use the same node classification and attachment preview decisions.
- Weak proof here invalidates ProjectStructure browser proof in later phases.

## Validation Depth

- Critical foundation.

## Implementation Steps

1. Reconfirm candidate helpers in `ProjectStructurePage.razor` and avoid moving service/stateful methods.
2. Add the helper class with internal static methods and strongly typed parameters.
3. Update the page to call the helper class.
4. Add or update unit tests for branch-heavy helper behavior if component tests do not cover it.
5. Run targeted ProjectStructure component tests.

## Scope Exceptions

- Do not move handlers that mutate page fields, persist canvas state, call injected services, or perform navigation.

## Do Not Do

- Do not split markup in this phase.
- Do not rename test ids or route text.
- Do not introduce fallback behavior for unsupported node kinds.

## Acceptance Checklist

- `ProjectStructurePage.razor` loses pure node helper logic.
- `ProjectStructureNodeHelpers` has typed methods and no service dependencies.
- ProjectStructure component tests pass.
- Downstream ProjectStructure component extraction is not blocked by helper coupling.

## Proof Required

- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProjectStructurePage`
- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter ProjectStructureNodeHelpers` if helper tests are added.
- Browser route smoke only if rendered output changes.

## Closure Evidence

- `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --filter ProjectStructureNodeHelpersTests --logger "console;verbosity=minimal"` passed with 4 tests.
- `dotnet test tests\CanDoItAll.Tests.Components\CanDoItAll.Tests.Components.csproj --filter ProjectStructurePage --logger "console;verbosity=minimal"` passed with 51 tests.
- Browser proof was not required for this subbundle because the extraction moved pure helper logic and did not intentionally change markup or layout.

## Browser Validation Logging

- Route: `/projects/{ProjectId:guid}/structure`.
- Viewport: `1600x900`.
- Required actions: navigate to a seeded project structure route after tests if helper output affects display labels or attachment preview.
- Screenshots: record only if rendered labels, attachment preview, or node details change.

## Progression Gate

- ProjectStructure tests pass and no moved helper depends on hidden page state. Then `02` may start.

## Suggested Agent Prompt

```text
Implement subbundle 01 only. Extract pure ProjectStructure node helpers into ProjectStructureNodeHelpers, preserve behavior, run the targeted tests, update the execution report, and stop if a helper depends on page state or injected services.
```
