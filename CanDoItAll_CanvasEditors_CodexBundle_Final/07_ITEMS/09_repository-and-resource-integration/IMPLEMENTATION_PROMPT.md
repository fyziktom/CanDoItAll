
# Implementation prompt

Implement **I09 — Repository nodes and resource integration**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Add repository node modes and metadata.
- Reuse or link resource entries when the same repository is already known in Resources.
- Implement UI for remote GitHub selection and local folder/path entry.
- Keep repository display concise on the canvas card while exposing full details in the inspector.

## Required design constraints

- Reuse CanDoItAll.Modules.Resources wherever repository-like references already exist.
- Remote GitHub repositories and local repositories should share one repository node family with mode-specific metadata.
- Folder selection should support browser capabilities but also provide a manual path fallback for unsupported environments.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ResourcesPageTests|ProjectStructurePageTests
- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
