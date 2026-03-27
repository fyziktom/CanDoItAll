
# Implementation prompt

Implement **I17 — Relationship editing, delete behavior, and borders**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Add clear unconnect and reconnect actions.
- Implement complexity-aware delete confirmation behavior.
- Enable drag-and-drop into named borders or frames and surface border naming controls.

## Required design constraints

- Differentiate simple-note deletion from higher-risk deletion that deserves confirmation.
- Treat borders as named grouping constructs rather than anonymous drop zones.
- Keep relationship editing explicit so reconnection is visible and reversible.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectStructurePageTests

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
