
# Specification

## Item identity

- **Item ID:** I04
- **Title:** Recording, transcript, and LLM-backed actions
- **Origin:** docx
- **Dependencies:** I01, I03

## Objective

Model recordings and transcripts as proper nodes and wrap all LLM-powered actions in explicit confirmation and provider selection.

## Normalized scope

Add Recording and Transcript nodes, transcript generation from recordings, standalone transcript support, and confirmed LLM actions such as Summarize, Find my tasks, and Find others delivery to me.

### In scope

- Recording node creation and placement beneath meetings or independently.
- Transcript node creation from recordings or manual standalone creation.
- LLM action confirmation dialog, provider selector, and result persistence.

### Out of scope

- A production-grade speech-to-text engine implementation beyond integration placeholders and provider orchestration.

## Key implementation decisions

- Recordings and transcripts should be first-class nodes, not hidden attachments.
- Every LLM request must ask for explicit confirmation and provider selection between OpenAI API and local Ollama.
- Reuse the existing workspace provider abstractions instead of inventing a new provider registry.

## Implementation tasks

- Add Recording and Transcript node families and editors.
- Add the transcript creation flow and standalone transcript creation path.
- Build a confirmation modal with provider selection for all LLM actions.
- Persist generated outputs or summaries back into nodes in a traceable way.
- Make the UI clearly communicate that an external or local provider request will be sent.

## Risks to control

- Hidden side effects if LLM actions can fire without confirmation.
- Provider duplication if workspace abstractions are ignored.

## Covered original notes

- N027 — Recording
- N028 — Usually under some meeting block
- N029 — Right click Menu options
- N030 — Create transcript
- N031 — Transcript
- N032 — Usually under some recording node, but can be separately (for example someone will send me transcript to email
- N033 — Right click menu options
- N034 — Summarize
- N035 — Find my tasks
- N036 — Find others delivery to me
- N037 — All those actions with confirmation because it must send request to LLM (selector OpenAI API vs Local Ollama)
