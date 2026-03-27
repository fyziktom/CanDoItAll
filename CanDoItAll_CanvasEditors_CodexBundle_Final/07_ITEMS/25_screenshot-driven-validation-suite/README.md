
# I25 — Screenshot-driven validation suite and evidence protocol

## Objective

Make screenshot-based validation a hard release gate for canvas-editor changes so visual regressions are not hand-waved away.

## Why this item exists

Add a dedicated screenshot validation protocol, naming convention, artifact checklist, and Playwright-first evidence strategy for all UI-changing items.

## Covered original notes

- No direct DOCX note mapping. This item exists because the user explicitly required cross-cutting validation or shared architecture.

## Dependencies

- I03 — Meeting nodes for online and onsite work
- I07 — Attachments, feedback, payment, and send flows
- I08 — Typed file nodes and Mermaid viewer
- I12 — .NET runtime, launch profile, and localhost nodes
- I14 — Remote server core model
- I15 — Domains, DNS, Docker, database, keys, and AI links
- I16 — Progress, priority, and marker UX normalization
- I17 — Relationship editing, delete behavior, and borders
- I18 — Arrow links, side-aware placement, and mindmap image export
- I19 — Progress summary modal, tree checklist, and exports
- I20 — Shared floating tool window host for canvas editors
- I21 — Prompt Factory components toolbox redesign
- I22 — Prompt Factory eye-preview popover
- I23 — Project Structure standard blocks toolbox
- I24 — Prompt Factory intermittent 44-node insertion bugfix

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
