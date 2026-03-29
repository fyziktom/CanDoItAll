# T04 — Toolbox functional repair and dedicated component extraction

## Phase
P0

## Goal
Fix the toolbox so it behaves correctly in the browser and move its markup/state into a dedicated component with explicit accordion/search semantics.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T00, T01

## Primary files
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.ToolWindows.cs`
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`

## Feature IDs that must remain green
F02, F03, F04, F05, F35, F36

## Implementation checklist
- Extract toolbox markup and logic into a dedicated component or clearly isolated child region.
- Implement explicit accordion state semantics: open, close, restore after search.
- Preserve existing test IDs or update tests in the same task.
- Ensure browser click behavior is proven, not only markup behavior in component tests.

## Validation
- Clicking a collapsed group opens it in browser and updates aria-expanded to true.
- Clicking the same open group collapses it and updates aria-expanded to false.
- Search opens matching groups according to the new spec and restores manual accordion state when cleared.
- Toolbox logic lives in a dedicated component or child partial instead of a large inline region in ProjectStructurePage.razor.

## Done when
- The toolbox no longer has a browser-only expand failure.
- Accordion behavior is explicitly defined and tested.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
