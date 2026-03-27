
# I14 — Remote server core model

## Objective

Model remote server infrastructure as a structured canvas node with technical, commercial, and access-related metadata.

## Why this item exists

Add remote server nodes with capacity, price, address, provider links, login links, SSH, secret references, and account identity.

## Covered original notes

- N100 — Remote Server (common block)
- N101 — Parameters
- N102 — CPU, RAM, HDD/SSD cap, etc.
- N103 — Price and business related info
- N104 — Address
- N105 — Provider
- N106 — Link to provider website
- N107 — Link to login
- N108 — SSH connection (we need terminal component)
- N109 — Connection to secret for login
- N110 — Account name

## Dependencies

- I01 — Foundation: rich node schema, metadata, and compatibility
- I10 — Script nodes and terminal execution surface

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
