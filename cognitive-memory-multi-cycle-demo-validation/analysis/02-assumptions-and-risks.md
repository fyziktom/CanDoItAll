# Assumptions And Risks

## Assumptions

- The Cognitive Memory API from the previous bundle remains available.
- PostgreSQL is available locally with the `candoitall` user used by the prior validation.
- Source file loading can be done through `POST /api/cognitive-memory/external-sources/files`.
- Project structure APIs can create or update Markdown asset nodes for email/instruction packets.
- Human-review decisions can be automated through `POST /api/cognitive-memory/review-items/{reviewItemId}/decisions` when the evidence is clear.

## Critical Path Risks

- If the stage loader bypasses APIs or writes directly to tables, the validation will not prove real behavior.
- If the same database from the prior bundle is reused, earlier memories can pollute source attribution and duplicate analysis.
- If the execution agent does not force consolidation after each stage, the observed cycle behavior will be ambiguous.
- If review decisions are bulk-approved without inspecting candidate previews, duplicate and contradiction behavior will not be validated.
- If bad behavior is discovered but no repair subbundle is created, final closure will hide the main point of this follow-up.

## Validation Risks

- Chat answers may appear correct because direct prompt context leaked source data, not because Cognitive Memory supplied the right memories.
- Vector/projection behavior may be unavailable in the developer profile; if so, execution must record the precise provider/profile limitation and still validate relational source selection, review, recall, and chat integration.
- Similar topics across projects can cause accidental cross-project leakage unless every memory is checked against project id and source locator.
- Email Markdown can produce noisy chunks; execution must distinguish useful instructions from transient message text.
- Contradiction cycles can produce duplicate candidates; execution must record whether the system proposes merge, supersession, duplicate, or new memory behavior.

## Reopen Triggers

- A source file is missing from the XLSX tracker or manifest.
- Any stage is loaded without API evidence.
- Any candidate or memory references the wrong project or wrong source file.
- A duplicate candidate is approved without a recorded reason.
- Chat answers use the right-sounding content but cannot be traced to correct memory/source evidence.
- Memory stores project links, file pointers, or staging metadata as durable memories.
- A discovered implementation defect is not represented by a new repair subbundle before final closure.
