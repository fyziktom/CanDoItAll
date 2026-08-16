# SB04 Semantic Invariants

## SB04-INV-01 — Exact active operation identity

- Source raw note: an active conversation must expose the exact operation identifier required for durable reconnect; Razor must not derive it.
- Expected behavior: `LlmChatConversationEngineState.ActiveOperationId` is the single source of truth and `HasActiveTurn` is derived from its presence. Both the runtime document mapper and EF read mapper wrap the persisted active turn id as `LlmChatOperationId`.
- Disallowed shallow implementation: an independently writable boolean, a generated identifier, a test-only property, or a Razor-side lookup.
- Failing-first evidence: `bundle://proof/SB04/transcripts/01-failing-first-active-operation.md`.
- Passing evidence: `bundle://proof/SB04/transcripts/03-focused-unit.md`, `bundle://proof/SB04/transcripts/05-focused-postgresql.md`.
- Changed sources: `repo://src/Modules/CanDoItAll.Modules.LlmChats/Ports/LlmChatExecutionPorts.cs`, `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/Runtime/LlmChatConversationEngine.cs`, and `repo://src/Modules/CanDoItAll.Modules.LlmChats.Persistence/ReadModels/EfLlmChatConversationReadStore.cs`.
- Production assertions: `bundle://proof/SB04/transcripts/11-source-assertions.md`.
- Red-team negative case: the PostgreSQL read test loads a second conversation and proves it cannot inherit the first conversation's operation id.
- Downstream dependency check: the Web project builds and the production PostgreSQL HTTP scenario observes the same exact id.

## SB04-INV-02 — Terminal clearing is authoritative

- Source raw note: terminal, compensated, and abandoned turns must clear the reconnect identity.
- Expected behavior: the shared conversation domain clears `ActiveTurn`; every engine-state mapper then emits `ActiveOperationId = null` and `HasActiveTurn = false`.
- Disallowed shallow implementation: clearing only UI state or only the HTTP property while persistence remains active.
- Failing-first evidence: `bundle://proof/SB04/transcripts/01-failing-first-active-operation.md`.
- Passing evidence: `bundle://proof/SB04/transcripts/03-focused-unit.md`, `bundle://proof/SB04/transcripts/05-focused-postgresql.md`.
- Changed sources: the engine-state contract and both production mappers listed by SB04-INV-01.
- Production assertions: the real PostgreSQL request-lifetime scenario reads the completed conversation through HTTP and proves the nullable field is omitted.
- Red-team negative case: completion, compensation, and abandonment are all exercised independently.
- Downstream dependency check: the inactive stub/API scenario retains the pre-existing `hasActiveTurn: false` contract while omitting only the new nullable member.

## SB04-INV-03 — Conversation and profile fences prevent identity leakage

- Source raw note: cross-conversation and cross-profile identity must be impossible.
- Expected behavior: active identity is read from the owning transcript row under the existing whole-use-case runtime lease. A profile generation change rejects the entire projection before it returns.
- Disallowed shallow implementation: process-global active-operation state, a profile-agnostic cache, or returning a stale successful result alongside an error.
- Failing-first evidence: `bundle://proof/SB04/transcripts/01-failing-first-active-operation.md`.
- Passing evidence: `bundle://proof/SB04/transcripts/03-focused-unit.md`, `bundle://proof/SB04/transcripts/05-focused-postgresql.md`.
- Changed sources: `repo://src/Modules/CanDoItAll.Modules.LlmChats/Application/LlmChatConversationContracts.cs` plus the authoritative state mappers.
- Production assertions: profile-switch integration retains the committed invocation audit and active turn but blocks stale reads and later writes.
- Red-team negative case: the application profile-scope test seeds an active projection, switches generation after the read, and asserts failure with no returned value.
- Downstream dependency check: transfer validation rejects an invalid active operation/invocation/event graph.

## SB04-INV-04 — Followers do not own operation cancellation

- Source raw note: the durable event session is the UI event source; follower disconnect/disposal must not cancel durable work and replay gaps must remain explicit.
- Expected behavior: session disposal releases only its runtime lease. Cancellation remains an explicit operation command. Durable reconnect returns journal events or a typed gap and closes only at terminal state.
- Disallowed shallow implementation: linking request/follower disposal to operation cancellation, redispatching on reconnect, or silently skipping a retention gap.
- Characterization-first evidence: `bundle://proof/SB04/transcripts/02-characterization-event-session.md`.
- Passing evidence: `bundle://proof/SB04/transcripts/03-focused-unit.md`, `bundle://proof/SB04/transcripts/05-focused-postgresql.md`.
- Changed test source: `repo://tests/Unit/CanDoItAll.Tests.Unit/LlmChatDurableStreamEventTests.cs`.
- Production assertions: the event-session production code remains unchanged; the new characterization drives its real factory, lease, journal, and operation repository.
- Red-team negative case: disposal leaves a running operation at cancellation generation zero.
- Downstream dependency check: the full-retention reconnect scenario proves durable high-water/gap behavior and one terminal close without redispatch.

## SB04-INV-05 — HTTP evolution is additive

- Source raw note: clients need the exact active operation id without breaking inactive-conversation payloads.
- Expected behavior: `activeOperationId` is a nullable JSON member omitted when inactive; all existing positional response members, including `hasActiveTurn`, remain unchanged.
- Disallowed shallow implementation: renaming/removing an existing property, serializing an empty GUID, or making clients infer the id from messages.
- Failing-first evidence: `bundle://proof/SB04/transcripts/01-failing-first-active-operation.md`.
- Passing evidence: `bundle://proof/SB04/transcripts/04-focused-api.md`, `bundle://proof/SB04/transcripts/05-focused-postgresql.md`.
- Changed sources: `repo://src/App/CanDoItAll.Web/Api/LlmChatApiContracts.cs` and `repo://src/App/CanDoItAll.Web/Api/LlmChatApiMapper.cs`.
- Production assertions: `bundle://proof/SB04/transcripts/11-source-assertions.md`.
- Red-team negative case: inactive JSON is asserted not to contain `activeOperationId`; active JSON must contain the exact non-empty operation GUID.
- Downstream dependency check: `CanDoItAll.Web` builds with zero warnings/errors and the existing bounded conversation API scenario passes.

## Production Behavior Artifact Matrix

| Artifact | Producer proof | Consumer proof | Lifecycle proof | Negative proof |
|---|---|---|---|---|
| `LlmChatConversationEngineState.ActiveOperationId` | Real admission persists `LlmConversationActiveTurn.TurnId`; runtime and EF mappers are asserted in `bundle://proof/SB04/transcripts/11-source-assertions.md` | Application details and Web mapper consume the typed property; production PostgreSQL HTTP proof is in `bundle://proof/SB04/transcripts/05-focused-postgresql.md` | Completion, compensation, abandonment, and profile switch are covered by focused unit/PostgreSQL runs | Failing-first compile proof plus unrelated-conversation and stale-profile negative cases |
| Durable operation event session | Existing operation journal/session factory in production | PostgreSQL SSE reconnect scenario in `bundle://proof/SB04/transcripts/05-focused-postgresql.md` | Session disposal releases its lease; terminal journal state closes the follower | Disconnect leaves operation running; full retention emits a typed gap; explicit cancellation tests remain green |
