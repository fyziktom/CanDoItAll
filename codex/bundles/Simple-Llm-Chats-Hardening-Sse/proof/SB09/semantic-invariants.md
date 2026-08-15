# SB09 semantic invariant contract

Changed-file hashes: `bundle://proof/SB09/changed-files.sha256`.
Positive commands: `bundle://proof/SB09/transcripts/01-current-head-gates.md`.
Negative/source commands: `bundle://proof/SB09/transcripts/02-negative-and-source-guards.md`.

## SBI-09-01 — asynchronous command-resource admission

- Expected behavior: every successful new or exact-replay turn request returns 202 promptly with one
  durable operation identity, Location, replay disposition, revision metadata, latest event sequence,
  and status/events/cancel links.
- Disallowed shallow implementation: await provider completion, return 200 for completed replay, map an
  admitted async provider failure into a synchronous HTTP failure, or create another request identity.
- Passing proof: a deterministic provider remains held after dispatch begins while POST has already
  returned 202; exact retry returns `replayed: true` and performs no second provider call.

## SBI-09-02 — durable replay is authoritative and gaps are explicit

- Expected behavior: SQL journal sequence is the replay cursor; Last-Event-ID and `after` are equivalent,
  gaps identify the retained window/resume cursor and authoritative status URL, and local signals only
  reduce polling latency.
- Disallowed shallow implementation: process-local-only replay, silent cursor reset, duplicate semantic
  text, full response buffering, or deriving status from the SSE connection.
- Passing proof: reconnect after the first delta omits it, emits the remaining text and terminal event,
  and keeps provider invocation count one. Deleted terminal history emits `stream.gap` while GET status
  remains authoritative.

## SBI-09-03 — projection lifetime never owns execution lifetime

- Expected behavior: SSE disconnect disposes only the captured read/profile lease; durable dispatch
  continues. Only the explicit cancel command changes operation cancellation state.
- Disallowed shallow implementation: link provider execution to `HttpContext.RequestAborted`, dispatch
  or cancel from the replay reader, infer cancellation from socket close, or automatically redispatch.
- Passing proof: the first connection closes after a delta, the operation succeeds, and reconnect
  resumes. A separate blocked operation changes to cancelled only after POST cancel and streams one
  cancellation terminal event.

## SBI-09-04 — typed bounded transport and terminal closure

- Expected behavior: versioned normalized event envelopes use durable sequence IDs and typed names;
  shared framing provides heartbeats/anti-buffering; success, failure, cancellation, and
  RecoveryRequired close immediately after their terminal event.
- Disallowed shallow implementation: raw provider frames/errors, internal wrapper serialization,
  multiple terminal events, WebSocket/query tokens, or continuing after terminal state.
- Passing proof: shared-writer tests observe public envelope JSON, dynamic names, heartbeat comments,
  proxy headers, terminal stop, and no post-terminal serialization; real PostgreSQL streams prove
  success/failure/cancellation closure.

## SBI-09-05 — profile fencing and sensitive-data exclusion

- Expected behavior: one product-owned stream session captures the current runtime identity before its
  first SQL read and closes/rejects reads after profile switch. API/SSE exposes assistant deltas and
  stable product metadata only.
- Disallowed shallow implementation: re-resolve profiles per poll, cross-profile reads, prompt/system
  instruction/credential/provider endpoint/raw error fields, or a Web dependency in product code.
- Passing proof: direct session and HTTP stream both close on switch; source guards find no forbidden
  dependencies/fields; real response/SSE assertions exclude the system prompt, user prompt, provider
  key, endpoint, and raw provider secret.

## Anti-stub result

The scoped production audit finds no partial extraction, test-only branch, TODO/FIXME,
`NotImplementedException`, dispatcher/invocation dependency in the SSE projection, or second replay
truth. Real-host tests use the production EF repository, journal, runtime lease, endpoint, and writer.
