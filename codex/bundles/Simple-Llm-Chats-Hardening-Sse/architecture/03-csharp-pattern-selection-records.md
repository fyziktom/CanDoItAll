# Pattern selection records

## PSR-01 — Transactional command store

**Selected:** focused command-store methods with one `AppDbContext` and explicit transaction.

**Why:** create, rename, admission, finalization and compensation each have cross-table invariants that
must commit together.

**Rejected:** ambient transaction, callback evidence sink, nested unit of work, or sharing state through
`AsyncLocal`.

## PSR-02 — Pure state reducer

**Selected:** pure reducer from durable operation/turn/attempt/cancellation evidence to the next legal
state.

**Why:** direct execution, restart reconciliation and explicit recovery must reach the same answer.

**Rejected:** duplicate switch statements in request and recovery services.

## PSR-03 — Durable claim lease

**Selected:** database CAS claim with owner ID, claim epoch/token, heartbeat and expiry.

**Why:** multiple app instances and restarts cannot rely on an in-memory registry.

**Rejected:** global static dictionary, distributed mutex without durable operation evidence, or
unconditional “recover if not local.”

## PSR-04 — Asynchronous dispatcher

**Selected:** admit transaction plus background dispatcher that claims work.

**Why:** provider work must outlive an HTTP request and support slow local models.

**Rejected:** fire-and-forget task started by endpoint, hosted queue with no durable source, or request
cancellation as the operation lifetime.

## PSR-05 — Streaming port plus completed fallback

**Selected:** additive streaming port and streaming provider capability. Non-streaming invocation remains
for existing workflow consumers.

**Why:** avoids breaking ordinary calls while enabling true incremental providers.

**Rejected:** changing `InvokeAsync` to return a stream everywhere or fabricating timed chunks.

## PSR-06 — Durable journal plus SSE projection

**Selected:** append/coalesce operation events transactionally; Web projects them through existing SSE
cursor/replay mechanics.

**Why:** reconnect and external clients need durable sequence and terminal truth.

**Rejected:** only in-memory broadcast, raw provider stream written directly to response, or transcript
rows per token.

## PSR-07 — Read models and keyset pagination

**Selected:** SQL projection queries independent of command aggregates.

**Why:** enterprise/chatbot traffic cannot materialize every transcript to list or page.

**Rejected:** repository `ListAsync` that reads every document and slices in memory.
