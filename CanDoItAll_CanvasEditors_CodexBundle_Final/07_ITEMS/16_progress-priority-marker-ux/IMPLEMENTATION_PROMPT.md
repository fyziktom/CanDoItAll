
# Implementation prompt

Implement **I16 — Progress, priority, and marker UX normalization**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Separate progress and priority interaction pathways clearly.
- Increase ring or badge sizes and update any hit testing if needed.
- Review accessibility and keyboard affordances for the status controls.

## Required design constraints

- Normalize the inconsistent note by using left-click progress badge for progress only and left-click priority badge for priority only.
- Keep marker selection separate rather than overloading one icon with multiple semantic meanings.
- Increase compact control diameter and hit targets for accessibility and reliability.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectStructureActionCatalogAdapterTests

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
