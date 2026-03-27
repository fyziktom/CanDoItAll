
# I04 — Recording, transcript, and LLM-backed actions

## Objective

Model recordings and transcripts as proper nodes and wrap all LLM-powered actions in explicit confirmation and provider selection.

## Why this item exists

Add Recording and Transcript nodes, transcript generation from recordings, standalone transcript support, and confirmed LLM actions such as Summarize, Find my tasks, and Find others delivery to me.

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

## Dependencies

- I01 — Foundation: rich node schema, metadata, and compatibility
- I03 — Meeting nodes for online and onsite work

## Files in this folder

- `README.md` — quick overview
- `SPECIFICATION.md` — normalized implementation scope
- `FILE_REFERENCES.md` — current code hotspots and likely new files
- `IMPLEMENTATION_PROMPT.md` — Codex implementation prompt for this item
- `VALIDATION_PROMPT.md` — QA and validation prompt for this item
- `ACCEPTANCE_CRITERIA.md` — pass or fail outcomes
- `CHECKLIST.md` — task checklist
- `SCREENSHOT_REQUIREMENTS.md` — screenshot evidence required for this item

## Delivery rule

This item is not complete until its acceptance criteria, test requirements, and screenshot requirements are all satisfied.
