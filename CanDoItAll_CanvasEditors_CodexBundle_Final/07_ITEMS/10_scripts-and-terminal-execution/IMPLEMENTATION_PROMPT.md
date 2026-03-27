
# Implementation prompt

Implement **I10 — Script nodes and terminal execution surface**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Add script node types and metadata.
- Create the open-terminal action and connect it to a safe runtime surface.
- Ensure the working directory and command path are visible in details.
- Provide enough execution feedback that users know what ran and where.

## Required design constraints

- Interpret Open terminal as an in-app terminal or command session, not a native OS terminal launch from the browser.
- Store working directory and command metadata explicitly on script nodes.
- Reuse manager/runtime helpers where possible for execution orchestration.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter WorkspaceRuntimeProcessToolsTests
- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
