# SB08 semantic invariant contract

Changed-file hashes: `bundle://proof/SB08/changed-files.sha256`.
Positive commands: `bundle://proof/SB08/transcripts/01-current-head-gates.md`.
Negative/source commands: `bundle://proof/SB08/transcripts/02-negative-and-source-guards.md`.

## SBI-08-01 — one durable append-only operation journal

- Expected behavior: every event is a normalized child of one durable operation and receives a unique,
  monotonically increasing sequence while the operation row is locked.
- Disallowed shallow implementation: process-local counters, JSON/raw-frame event blobs, a second
  operation table, or sequence assignment outside the transaction.
- Passing proof: eight concurrent PostgreSQL contexts append to one operation and read back a unique
  contiguous sequence; a fresh context replays committed events without shared memory.
- Production assertion: `EfLlmChatOperationEventRepository.AppendAsync` uses `FOR UPDATE`, `MAX + 1`,
  and the `(OperationId, Sequence)` primary key. `EfLlmChatUnitOfWork` rejects modification/deletion of
  tracked event rows.
- Downstream dependency: SB09 may page by sequence and use the in-process signal only as an accelerator.

## SBI-08-02 — atomic lifecycle evidence and post-commit wakeup

- Expected behavior: admission, lease, attempt, cancellation, recovery, transcript success, and actual
  terminal state events commit with the durable state they describe. A local wakeup occurs only after
  the outer transaction commits.
- Disallowed shallow implementation: publish before commit, use another DbContext, or persist an event
  after its state transaction.
- Passing proof: PostgreSQL commit/rollback tests observe one wake only after commit and no wake after
  rollback; transaction tests retain the existing transcript/operation atomicity union.
- Production assertion: nested journal calls join `EfLlmChatUnitOfWork`; callbacks are drained only
  after `CommitAsync`. `RecoveryRequired` remains nonterminal and carries its named failure evidence,
  while invocation events retain known usage.

## SBI-08-03 — bounded time-aware text coalescing

- Expected behavior: provider deltas are aggregated and durably flushed on minimum size, natural text
  boundary, maximum UTF-8 bytes, or the configured time window.
- Disallowed shallow implementation: one row per token, broken surrogate/rune boundaries, unbounded
  aggregate response memory, or leaving a small delta buffered through a provider pause.
- Passing proof: direct tests combine small deltas, split without corrupting an emoji, and observe a
  small delta in the journal while the provider stream is still paused.
- Production assertion: every delta append requires the current durable execution lease; aggregate
  characters/bytes and durable event count are capped with `llm-chat.stream-limit-exceeded`.

## SBI-08-04 — partial evidence is replayable but noncanonical

- Expected behavior: partial output survives provider failure for replay, is labelled incomplete by
  the failed/cancelled state, and never becomes an assistant transcript message unless final success
  commits.
- Disallowed shallow implementation: discard partial evidence, finalize partial text, or expose raw
  provider exceptions/frames/credentials as event payload.
- Passing proof: the failure pipeline persists `partial output`, records a stable failed state with
  usage, compensates the active turn, and commits no assistant message.
- Production assertion: the executor has only `StreamTurnAsync`; the public/concrete completed-only
  `InvokeTurnAsync` bypass is removed. Event rows contain only typed state, attempt, text, model, usage,
  stable failure code, sequence, and timestamp.

## SBI-08-05 — explicit retention and portable transfer

- Expected behavior: only journals of terminal operations older than the retention boundary are
  deleted in bounded operation batches. Active, cancellation-requested, and recovery-required journals
  remain. Transfer includes retained event/audit data.
- Disallowed shallow implementation: delete active evidence, time retention from event timestamps,
  omit retained journals from export, or accept detached/malformed event variants during import.
- Passing proof: PostgreSQL retention deletes expired terminal events while preserving active events;
  the transfer round trip includes event rows and validates ownership, sequence, variants, UTF-8 bounds,
  and stable failure codes.

## Anti-stub result

The scoped production audit finds no TODO/FIXME, `NotImplementedException`, test-only branch, fixture,
or stub marker. PostgreSQL concurrency/rollback/retention/transfer tests use the production EF adapter;
the unit stream tests exercise the production pipeline, journal, lease check, and coalescer.
