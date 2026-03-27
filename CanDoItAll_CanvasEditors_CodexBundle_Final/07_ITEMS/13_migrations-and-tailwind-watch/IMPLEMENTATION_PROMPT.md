
# Implementation prompt

Implement **I13 — EF migrations and Tailwind watch nodes**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Add migration and Tailwind node subtypes and metadata.
- Define project-aware command defaults and manual override fields.
- Wire the nodes to the shared terminal execution path.

## Required design constraints

- Reuse the same execution surface created for scripts and runtime nodes.
- Store commands explicitly and transparently so users can inspect and adjust them.
- Support project-aware defaults but allow manual overrides.

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
