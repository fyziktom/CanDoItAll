# Durable SSE and replay

## Two layers

1. Durable product event journal in PostgreSQL.
2. Transport projection through existing `ServerSentEventResponseWriter` and profile-bounded replay.

The in-memory replay stream accelerates live delivery. PostgreSQL is the reconnect/source-of-truth
fallback for operation events.

## Sequence

Each operation has a strictly increasing `Sequence`. Append/coalesce is atomic with the operation
transition it describes. A unique key enforces `(OperationId, Sequence)`.

A global/profile stream sequence may be used by the generic replay helper, but the client contract must
also expose stable per-operation sequence/cursor semantics.

## Reconnect

- Client sends `Last-Event-ID` or documented `after`.
- Server first loads missing durable events after the cursor.
- It then attaches to live delivery without a race by using a snapshot/high-water protocol.
- Duplicate delivery is allowed only when IDs let clients deduplicate; missing delivery is not.
- If retention removed the cursor range, emit `stream.gap` plus a current operation snapshot/resume
  cursor.
- Terminal event closes the response after flush.

## Disconnect

A disconnected SSE reader is not an operation cancellation request. It only cancels the HTTP stream.

## Security/redaction

Events never include:

- system prompt;
- full user prompt unless the API contract explicitly grants it through another endpoint;
- credentials, endpoints or headers;
- raw provider exception;
- hidden chain-of-thought/reasoning;
- unrelated profile/organization data.

Text deltas are the assistant output requested by the authorized caller.
