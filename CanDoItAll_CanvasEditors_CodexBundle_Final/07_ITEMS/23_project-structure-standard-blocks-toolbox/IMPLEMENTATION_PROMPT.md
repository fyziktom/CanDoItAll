
# Implementation prompt

Implement **I23 — Project Structure standard blocks toolbox**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Move or duplicate standard block creation into the shared floating toolbox host.
- Render blocks in a searchable tree-style grouping.
- Keep creation behavior intact and clearly indicate the currently selected source node if relevant.

## Required design constraints

- Use the same shared tool-window host as Prompt Factory to avoid duplicate UX systems.
- Prefer tree-style grouping over the current in-inspector accordion stack for long block lists.
- The toolbox should complement or replace the old accordion area without creating two competing primary flows.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectStructurePageTests
- dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
