
# Implementation prompt

Implement **I02 — Common starter blocks and project structure catalog refresh**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Add starter block catalog entries and subtype mappings.
- Define node visuals and seed labels for each new block.
- Ensure creation from the canvas produces the right type, subtype, and placement defaults.
- Update any outline or navigation views that list block families.

## Required design constraints

- Treat these as starter or grouping nodes rather than heavyweight domain entities.
- Keep the catalog grouped and searchable so future block families stay manageable.
- Prefer reuse of existing visual profile mechanisms over one-off CSS classes.

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
