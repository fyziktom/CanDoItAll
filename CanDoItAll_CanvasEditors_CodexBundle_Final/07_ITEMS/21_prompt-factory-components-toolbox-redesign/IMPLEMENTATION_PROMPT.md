
# Implementation prompt

Implement **I21 — Prompt Factory components toolbox redesign**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Replace the transient or wrong component toolbox presentation with the shared floating tool-window host.
- Render component groups as a tree or equivalent dense hierarchy rather than accordions.
- Keep search bar fixed at the top and the component list independently scrollable.
- Ensure adding a component from the toolbox still routes through the proper catalog action pipeline.

## Required design constraints

- Treat the existing toolbox-panel implementation as a reference point, not the final UX.
- Prefer a dense tree-view or outline experience over stacked accordions for large component catalogs.
- Keep search pinned at the top and content scrollable within the window body.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter PromptFactoryCatalogToolboxTests|PromptFactoryPageTests
- dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj --filter PromptLibraryVerificationTests

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
