
# Specification

## Item identity

- **Item ID:** I07
- **Title:** Attachments, feedback, payment, and send flows
- **Origin:** docx
- **Dependencies:** I01, I06

## Objective

Model delivery evidence and operational follow-up items explicitly on the canvas instead of burying them in generic notes.

## Normalized scope

Add typed attachment and follow-up nodes for video, screenshot, log, notes, revision, feedback, payment, and send with channel-aware options.

### In scope

- Video, screenshot, log, notes, revision, feedback, payment, and send node families.
- Send selector options such as File, Offer, Email, Message plus channel, Invoice, and Money.
- Attachment capture entry points and previews where appropriate.

### Out of scope

- Full email delivery infrastructure or accounting software integration.

## Key implementation decisions

- Attachment-like and follow-up nodes should share common metadata but retain clear subtypes.
- Screenshot acquisition should support clipboard and recent-capture fallback rather than requiring a custom desktop integration first.
- Send and Payment nodes capture structured intent and status; they do not have to implement every external sending or billing integration immediately.

## Implementation tasks

- Add typed nodes and card visuals for each requested attachment or follow-up category.
- Implement screenshot import via clipboard or recent capture fallback.
- Add structured selectors for Send and Payment related choices.
- Support lightweight preview or link-out behavior for relevant attachment types.

## Risks to control

- Confusing overlap between files, attachments, and generic notes if subtype visuals are weak.

## Covered original notes

- N055 — Video
- N056 — Screenshot (take last captured screenshot, or from clipboard)
- N057 — Log
- N058 — notes
- N059 — Revision
- N060 — Feedback
- N061 — Payment
- N062 — Send
- N063 — With selector for File, Offer, Email, Message (plus channel), invoice, money, etc.
