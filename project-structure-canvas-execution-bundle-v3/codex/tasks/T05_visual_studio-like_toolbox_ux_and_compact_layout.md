# T05 — Visual Studio-like toolbox UX and compact layout

## Phase
P0

## Goal
Make the toolbox feel like a Visual Studio toolbox: compact, list-like, single-line items, icon plus label, one open group at a time (unless search is active), and hover tooltip with action description.

## Why this task exists
This task is required to unblock the later real-canvas migration safely and to reduce the risk of breaking existing product behavior.

## Depends on
T04

## Primary files
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor.css`
- `src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor`
- `tests/CanDoItAll.Tests.Playwright/AppSmokeTests.cs`
- `references/visual-studio-toolbox-reference.png`

## Feature IDs that must remain green
F02, F03, F04, F05, F35, F36

## Implementation checklist
- Redesign toolbox rows as a compact list with one-line label text and left icon.
- Move description text into `title`/tooltip metadata.
- Make search sticky and section scrolling local to the toolbox body.
- Tune CSS toward a compact Visual Studio-like list instead of card-like blocks.

## Validation
- Rows are visually single-line with icon and label only; description is available via title/tooltip.
- Search input stays sticky at the top while groups scroll.
- Keyboard Enter/Space on group headers works.
- Playwright screenshots prove the compact list layout in default, expanded, and search states.

## Done when
- The toolbox no longer looks like stacked cards; it behaves like a compact grouped list.
- Hover reveals the item description without expanding the row height.

## Notes
- Keep comments in source code in English.
- If the task changes shared canvas code, also validate PromptFactory and relevant sandbox surfaces.
- Do not suppress failing tests to get past this task.
