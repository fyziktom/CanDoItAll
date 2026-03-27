
# Implementation prompt

Implement **I20 — Shared floating tool window host for canvas editors**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Generalize or extend the floating inspector host into a reusable tool-window host.
- Add pin, move, bounds-clamp, and scroll behavior.
- Support consistent header and body slots for search, tree content, and previews.
- Ensure the host is visually safe on smaller canvases and within the visible stage.

## Required design constraints

- Build a shared host instead of implementing separate ad-hoc floating windows for Prompt Factory and Project Structure.
- The host must support show or hide, pin, drag, bounds clamping, and internal scrolling.
- Toolbar window behavior should feel closer to Visual Studio Solution Explorer than to temporary accordions or transient context menus.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter FloatingInspectorHostTests|CanvasWorkbenchTests
- dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
