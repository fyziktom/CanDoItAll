
# Implementation prompt

Implement **I07 — Attachments, feedback, payment, and send flows**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Add typed nodes and card visuals for each requested attachment or follow-up category.
- Implement screenshot import via clipboard or recent capture fallback.
- Add structured selectors for Send and Payment related choices.
- Support lightweight preview or link-out behavior for relevant attachment types.

## Required design constraints

- Attachment-like and follow-up nodes should share common metadata but retain clear subtypes.
- Screenshot acquisition should support clipboard and recent-capture fallback rather than requiring a custom desktop integration first.
- Send and Payment nodes capture structured intent and status; they do not have to implement every external sending or billing integration immediately.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ClipboardBridgeTests|ProjectStructurePageTests

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
