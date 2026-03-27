
# Implementation prompt

Implement **I08 — Typed file nodes and Mermaid viewer**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Create subtype mappings for the requested file types.
- Apply stable color and icon tokens for each subtype.
- Add Mermaid detection and viewer affordances.
- Expose detected diagram type on the node or in details.

## Required design constraints

- Represent files through one file-family model with deterministic subtype-to-color and subtype-to-icon mapping.
- Treat Mermaid as a special file-like subtype with viewer support and automatic diagram type detection.
- Keep file color semantics consistent across canvas cards, inspectors, and previews.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
