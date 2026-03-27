
# Implementation prompt

Implement **I03 — Meeting nodes for online and onsite work**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Add meeting-specific metadata fields and editors.
- Expose online channel options such as MSTeams, Google Meet, Zoom, WhatsApp, and Telegram.
- Implement onsite address behavior with map link support.
- Integrate repeating metadata into meeting details and any schedule-related views.
- Ensure meeting actions appear only where they make sense.

## Required design constraints

- Use one Meeting node family with a mode subtype or metadata field instead of separate disconnected models.
- Leverage StartUtc and EndUtc integration with the existing project calendar where possible.
- Represent repeating cadence as normalized metadata rather than free-form text.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectCalendarPageTests|ProjectStructurePageTests
- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
