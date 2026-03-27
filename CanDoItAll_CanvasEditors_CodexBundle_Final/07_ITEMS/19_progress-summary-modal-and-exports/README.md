
# I19 — Progress summary modal, tree checklist, and exports

## Objective

Turn nested node progress into a real summary view that can also be exported.

## Why this item exists

Add a progress summary modal showing a tree of child statuses, inline status editing, XLSX export, and Mermaid Gantt export.

## Covered original notes

- N136 — Controls
- N137 — Progress summary
- N138 — For nodes that have some nodes under it automated display of summary checklist of state items under it
- N139 — Click to button in props panel or right-click menu item => open modal with summary status, checklist of all statuses of items under it (as tree view), possibility to change status in that list (each item has on its line button with dropdown selector of progress)
- N140 — Posibility to export as xlsx
- N141 — Export as mermaid gantt graph

## Dependencies

- I06 — Task, issue, and assignment model
- I16 — Progress, priority, and marker UX normalization
- I18 — Arrow links, side-aware placement, and mindmap image export

## Files in this folder

- `README.md` — quick overview
- `SPECIFICATION.md` — normalized implementation scope
- `FILE_REFERENCES.md` — current code hotspots and likely new files
- `IMPLEMENTATION_PROMPT.md` — Codex implementation prompt for this item
- `VALIDATION_PROMPT.md` — QA and validation prompt for this item
- `ACCEPTANCE_CRITERIA.md` — pass or fail outcomes
- `CHECKLIST.md` — task checklist
- `SCREENSHOT_REQUIREMENTS.md` — screenshot evidence required for this item

## Delivery rule

This item is not complete until its acceptance criteria, test requirements, and screenshot requirements are all satisfied.
