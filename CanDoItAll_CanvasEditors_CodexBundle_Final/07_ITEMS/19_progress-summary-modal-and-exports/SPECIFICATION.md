
# Specification

## Item identity

- **Item ID:** I19
- **Title:** Progress summary modal, tree checklist, and exports
- **Origin:** docx
- **Dependencies:** I06, I16, I18

## Objective

Turn nested node progress into a real summary view that can also be exported.

## Normalized scope

Add a progress summary modal showing a tree of child statuses, inline status editing, XLSX export, and Mermaid Gantt export.

### In scope

- Progress summary entry points from the props panel and right-click menu.
- Tree view modal with inline progress selectors.
- XLSX export.
- Mermaid Gantt export.

### Out of scope

- Full project management analytics beyond the requested summary and exports.

## Key implementation decisions

- Summaries should be computed from the existing hierarchy rather than maintained as disconnected manual counters.
- Status edits inside the modal must write back to the underlying nodes.
- Exports should reflect the same underlying hierarchy and status values shown in the modal.

## Implementation tasks

- Compute a hierarchical summary of progress states under a node.
- Add modal UI with tree view and inline selectors.
- Implement XLSX export and Mermaid Gantt export.
- Keep exports aligned with the on-screen summary model.

## Risks to control

- Summary and node state drift apart if the modal edits do not round-trip properly.

## Covered original notes

- N136 — Controls
- N137 — Progress summary
- N138 — For nodes that have some nodes under it automated display of summary checklist of state items under it
- N139 — Click to button in props panel or right-click menu item => open modal with summary status, checklist of all statuses of items under it (as tree view), possibility to change status in that list (each item has on its line button with dropdown selector of progress)
- N140 — Posibility to export as xlsx
- N141 — Export as mermaid gantt graph
