
# I12 — .NET runtime, launch profile, and localhost nodes

## Objective

Make .NET project runtime nodes truly useful by connecting them to launch profiles, project selection, localhost URLs, and run modes.

## Why this item exists

Implement .NET-related nodes that parse launchSettings, infer default addresses, expose localhost links, and support dotnet watch and release run variants.

## Covered original notes

- N083 — Dotnet related
- N084 — Project default launch profile
- N085 — Project selector
- N086 — Then auto parse of launchprofile to get default addresses
- N087 — localhost run in node details – click to open in new tab
- N088 — Dotnetwatch
- N089 — Command to run specific project in dotnetwatch
- N090 — Ideal would be project selector
- N091 — Specify http vs https
- N092 — Run Release
- N093 — Specify http vs https
- N094 — Ideal would be project selector
- N095 — Address of release localhost run in node details – click to open in new tab

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
