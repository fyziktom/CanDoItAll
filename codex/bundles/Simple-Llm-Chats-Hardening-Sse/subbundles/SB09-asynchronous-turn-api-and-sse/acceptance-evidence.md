# Acceptance evidence — SB09

- [x] Turn start returns 202 without waiting for provider completion.
- [x] SSE delivers ordered deltas and exactly one terminal operation event.
- [x] Reconnect resumes without duplicate semantic text or a second provider call.
- [x] A replay gap emits stream.gap with a usable recovery cursor while status remains authoritative.
- [x] SSE disconnect does not cancel or abandon the operation.
- [x] Explicit cancellation is visible in operation status and event stream.
- [x] The stream closes after terminal success, failure, cancellation, or RecoveryRequired.
- [x] Existing anti-buffering, heartbeat, cursor, and profile-lifetime behavior is reused.

## Required semantic proof

- Intended case: POST returns 202 while a deterministic provider is still held; an SSE subscriber reads
  a durable delta, reconnects with Last-Event-ID, receives only later text and one success event, and
  observes one provider call.
- Negative/race/crash/failure case: response disposal does not cancel execution; conflicting cursors
  return a stable 400; retained-history deletion emits an explicit gap; explicit cancellation and
  provider failure each produce one closing event; profile switch closes both the direct product
  session and HTTP projection.
- Why the old implementation would fail this proof: before SB09, POST could synchronously return 200
  or map a terminal failure, response metadata had no replay/event links or last sequence, and no
  durable operation SSE endpoint, typed envelope, gap projection, or terminal-close contract existed.
- Exact source owner: `LlmChatOperationEventStreamSession` owns durable profile-fenced reads;
  `EfLlmChatOperationEventRepository` owns SQL page/range/aggregate queries; Web owns the typed mapper,
  replay adapter, routes, and generic writer extension.
- Exact command(s): the expected-red single PostgreSQL test; final Web build; the 22-case focused
  LlmChats/API transport union; the exact two-case correction rerun; source guards; CodeAnalytics
  snapshot; and the bundle validator set.
- Actual result: affected build 0 warnings/errors; 20 unchanged cases plus 2 corrected cases pass;
  zero architecture cycles/diagnostics/open questions; all source and bundle guards pass.
- Evidence artifact: `bundle://proof/SB09/manifest.md` and its transcripts/invariant/hash artifacts.
- Commit SHA: `4c71bfa8857d1228e5cb5e23fac44c9746954dfc`.
