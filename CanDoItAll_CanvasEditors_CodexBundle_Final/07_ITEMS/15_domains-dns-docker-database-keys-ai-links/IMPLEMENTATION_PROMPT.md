
# Implementation prompt

Implement **I15 — Domains, DNS, Docker, database, keys, and AI links**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Define typed child nodes or metadata shapes for the requested infrastructure concepts.
- Ensure the server subtree stays readable and navigable.
- Add node editors and concise card summaries for the new infrastructure children.

## Required design constraints

- Prefer typed child nodes beneath infrastructure roots over squeezing everything into one giant server card.
- Reuse resource-like concepts such as DockerCompose, Ssh, SecretLink, or PromptLink where that reduces duplication.
- AI links are references and context anchors, not embedded conversations.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj
- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
