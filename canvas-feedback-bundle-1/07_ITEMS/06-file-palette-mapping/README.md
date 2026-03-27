# Item 06: File palette mapping

## Covered notes

- `N013`

## Objective

Stop treating every file node like a generic document and restore subtype-specific background palettes in the structure graph.

## Execution checklist

- Resolve file palettes from `ObjectSubtype` in the graph adapter.
- Map the validated file families to explicit palette keys.
- Add component coverage for the adapter mapping.
- Re-check the browser node data for PDF, spreadsheet, and document files.

## Implemented in

- `src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureGraphAdapter.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructureGraphAdapterTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Validation

- `ProjectStructureGraphAdapterTests`
- `Project_structure_feedback_fixes_are_validated_in_browser`

## Status

`Done and validated`
