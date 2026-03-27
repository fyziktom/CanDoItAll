
# Implementation prompt

Implement **I04 — Recording, transcript, and LLM-backed actions**.

## Start here

1. Read `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Read this folder's `SPECIFICATION.md`, `FILE_REFERENCES.md`, `ACCEPTANCE_CRITERIA.md`, and `SCREENSHOT_REQUIREMENTS.md`.
4. Inspect the existing files listed in `FILE_REFERENCES.md`.

## What you must deliver

- Add Recording and Transcript node families and editors.
- Add the transcript creation flow and standalone transcript creation path.
- Build a confirmation modal with provider selection for all LLM actions.
- Persist generated outputs or summaries back into nodes in a traceable way.
- Make the UI clearly communicate that an external or local provider request will be sent.

## Required design constraints

- Recordings and transcripts should be first-class nodes, not hidden attachments.
- Every LLM request must ask for explicit confirmation and provider selection between OpenAI API and local Ollama.
- Reuse the existing workspace provider abstractions instead of inventing a new provider registry.

## Validation work you must add before closing

- Extend or add the relevant tests listed below.
- Capture the screenshots required by `SCREENSHOT_REQUIREMENTS.md`.
- Produce a short evidence summary that links the implemented behavior to the acceptance criteria.

## Suggested test commands

- dotnet test tests/CanDoItAll.Tests.Components/CanDoItAll.Tests.Components.csproj --filter ProjectStructurePageTests
- dotnet test tests/CanDoItAll.Tests.Integration/CanDoItAll.Tests.Integration.csproj

## Refusal conditions

Do **not** mark this item complete if:
- the implementation bypasses normalized decisions,
- the requested screenshots do not exist,
- tests were not updated where behavior changed,
- a visual canvas change cannot be demonstrated,
- the implementation quietly creates duplicate registries or impossible browser behavior.
