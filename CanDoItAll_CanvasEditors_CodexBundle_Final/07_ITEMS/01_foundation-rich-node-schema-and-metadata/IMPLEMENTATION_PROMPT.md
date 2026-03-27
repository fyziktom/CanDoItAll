
# Implementation prompt

Implement **I01 — Foundation: rich node schema, metadata, and compatibility**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Define the new node-family strategy and codify it in shared contracts.
- Add typed metadata DTOs and safe serialization helpers.
- Extend persistence schema and service logic to round-trip metadata cleanly.
- Provide migration or defaulting logic so existing records continue to load without metadata.
- Add validation that rejects malformed metadata and unknown critical subtypes where appropriate.

## Required design constraints

- Add a structured metadata payload such as MetadataJson to project objects instead of adding dozens of dedicated columns.
- Add only a small set of new ProjectObjectType values for real behavioral families such as Meeting, Recording, Transcript, Participant, WorkItem, Script, Environment, and Infrastructure.
- Use ObjectSubtype and typed metadata DTOs for specialized variants such as online meeting, onsite meeting, HR participant, DNS record, Tailwind watch, or ChatGPT link.
- Keep all existing nodes working without migration surprises; the new metadata strategy must be additive and backward-compatible.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj --filter ProjectWorkbenchServiceIntegrationTests
- dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
