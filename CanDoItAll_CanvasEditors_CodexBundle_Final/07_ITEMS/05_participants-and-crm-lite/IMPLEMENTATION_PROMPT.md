
# Implementation prompt

Implement **I05 — Participants and CRM-lite registry**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Define participant metadata and visual variants.
- Create a lightweight registry or selector source for participant reuse.
- Support organization-chart-like grouping with team blocks and team sections.
- Ensure AI Agent can participate anywhere a person-like assignee or participant is allowed when semantically appropriate.

## Required design constraints

- Do not implement a full CRM module; implement a lightweight participant registry and references.
- Use the same participant objects across meetings, tasks, and org-chart-like canvas structures.
- Model AI Agent as a participant-like entity with a distinct subtype and iconography.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj
- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectStructurePageTests

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
