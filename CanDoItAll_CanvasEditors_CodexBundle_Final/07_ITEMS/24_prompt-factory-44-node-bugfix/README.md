
# I24 — Prompt Factory intermittent 44-node insertion bugfix

## Objective

Root-cause and fix the intermittent bug where a single component insertion sometimes attempts to add dozens of nodes.

## Why this item exists

Instrument, reproduce, and eliminate the intermittent duplicate-add behavior in Prompt Factory, with a regression harness that proves the fix.

## Covered original notes

- N152 — Bugs:
- N153 — Adding of any component wants to add 44 nodes (happens just sometimes, like 4/5 situations).

## Dependencies

- I21 — Prompt Factory components toolbox redesign

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
