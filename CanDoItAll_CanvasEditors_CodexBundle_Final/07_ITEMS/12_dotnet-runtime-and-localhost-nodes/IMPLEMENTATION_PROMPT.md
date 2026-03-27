
# Implementation prompt

Implement **I12 — .NET runtime, launch profile, and localhost nodes**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Create .NET runtime node subtypes and details editors.
- Add project selector and launch profile parsing integration.
- Surface inferred localhost URLs in node details as clickable links.
- Implement dotnet watch and release node variants with protocol options.

## Required design constraints

- Reuse the existing LaunchProfileSettingsResolver instead of rebuilding launch profile parsing.
- Treat dotnet watch and release run as runtime variants with shared project selection behavior.
- Expose URL choices clearly and make localhost addresses clickable from node details.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Unit/CanDoItAll.Tests.Unit.csproj --filter LaunchProfileSettingsResolverTests|WorkspaceRuntimeProcessToolsTests
- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
