
# Implementation prompt

Implement **I25 — Screenshot-driven validation suite and evidence protocol**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Define artifact names and storage layout per item.
- Require screenshot capture and short semantic analysis for all UI items.
- Expand Playwright coverage where the current suite is too thin for the requested canvas changes.

## Required design constraints

- Any item that changes the canvas or toolbox UI must produce screenshots and a short semantic analysis, not only passing tests.
- Prefer automated Playwright captures where practical, then supplement with manual evidence if the scenario is hard to script.
- A task is not done if screenshot evidence is missing or obviously does not show the claimed behavior.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
