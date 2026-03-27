
# Implementation prompt

Implement **I24 — Prompt Factory intermittent 44-node insertion bugfix**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Add instrumentation or logging around component-add dispatch.
- Create a reliable reproduction or a bounded stress harness.
- Fix the root cause and add defensive deduplication where justified.
- Add regression tests that would fail on the old behavior.
- Document the root cause in the validation evidence.

## Required design constraints

- Do not close this item without root-cause evidence; symptom-only patches are not enough.
- Assume the fault may live in event dispatch, repeated submissions, or interop duplication rather than only in the final add method.
- Guard the action pipeline against duplicate submissions where appropriate.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter PromptFactoryCatalogToolboxTests|PromptFactoryPageTests
- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter PromptFactoryServiceIntegrationTests
- dotnet test tests/CanDoItAll.Tests.Playwright/CanDoItAll.Tests.Playwright.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
