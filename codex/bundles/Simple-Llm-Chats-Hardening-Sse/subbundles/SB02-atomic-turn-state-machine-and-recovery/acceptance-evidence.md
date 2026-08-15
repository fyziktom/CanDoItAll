# Acceptance evidence — SB02

- [x] Turn admission is one transaction across operation, transcript, admission evidence, and the future event-join seam.
- [x] Successful completion is one transaction across assistant message, usage, active-turn clearing, and operation success.
- [x] Failed compensation cannot leave a terminal Failed or Cancelled operation with a live active turn.
- [x] A cancellation request committed before finalization prevents Succeeded.
- [x] Same operation ID and fingerprint replays the original result even after later lifecycle changes.
- [x] Same operation ID with a different fingerprint conflicts before provider dispatch.
- [x] Conversation archive cannot race an active or nonterminal turn.
- [x] Direct completion and recovery reduce identical durable evidence to the same outcome.

## Required semantic proof

- Intended case: explicit admission, provider invocation, and finalization phases use one deterministic
  protocol; provider I/O is outside transactions and each state mutation is atomic.
- Negative/race/crash/failure case: real PostgreSQL failure injection proves rollback at admission,
  success, and compensation; the historical cancellation regression fails 0/1 before SB02 and passes
  1/1 afterward; archive and dispatch claims use database locks.
- Why the old implementation would fail this proof: it committed transcript changes before evidence,
  allowed `CancellationRequested -> Succeeded`, swallowed compensation exhaustion, and validated mutable
  lifecycle state before replay identity.
- Exact source owner: `LlmChatOperationAdmissionService`, `LlmChatOperationStateMachine`,
  `LlmChatOperationDetailsReader`, and `LlmChatOperationReducer`.
- Exact commands: recorded in `proof-manifest.json` and `proof/SB02/transcripts`.
- Actual result: focused Unit 19/19 plus regression 1/1; PostgreSQL 4/4; real-host API 1/1; affected builds
  and EF model check pass.
- Evidence artifact: `proof/SB02/manifest.md`.
- Commit SHA: `be36fedb2ce329af6021cd2330eb6162d8ef2db4`.

SB08 remains the explicit owner of the additive durable stream-event journal and will join lifecycle
event rows to these transaction seams without changing transcript authority.
