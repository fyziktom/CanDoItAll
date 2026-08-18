# Acceptance evidence — SB08

- [x] Every operation event has a unique monotonic sequence within its operation.
- [x] State-transition events commit in the same transaction as their state.
- [x] Text chunks are coalesced and bounded rather than one row per token.
- [x] Partial output is replayable but never canonical unless finalization succeeds.
- [x] A second instance reads all committed events without first-instance memory.
- [x] Event payloads contain no system prompt, user prompt, credential, or raw provider error.

## Required semantic proof

- Intended case: a claimed turn produces attempt/state/text events, coalesces small UTF-8 deltas,
  completes the transcript once, and exposes a unique ordered journal to any database instance.
- Negative/race/crash/failure case: eight concurrent writers retain unique ordering; rollback publishes
  no wake; provider pause flushes by deadline; partial provider failure keeps incomplete replay evidence,
  compensates the active turn, and creates no assistant message; retention preserves active journals.
- Why the old implementation would fail this proof: before SB08 there was no operation event table,
  sequence owner, post-commit signal, coalescer, replay page, retention policy, or stream-to-transcript
  finalization pipeline; the executor invoked the completed-only method directly.
- Exact source owner: product event/application journal and streaming pipeline; Persistence EF repository,
  unit of work, migration, transfer handler, audited/profile-fenced streaming ports.
- Exact command(s): final 61-case `FullyQualifiedName~LlmChat` Unit filter, final 7-case PostgreSQL
  journal/turn/transfer filter, EF pending-model check, source guards, and CodeAnalytics snapshot.
- Actual result: all behavioral checks pass; no pending model changes; zero cycles/blocking errors/error
  findings/open questions; one documented nonblocking existing engine file-size warning.
- Evidence artifact: `bundle://proof/SB08/manifest.md` and its transcripts/invariant/hash artifacts.
- Commit SHA: `e543e7bdd3de97e8f52db9d7df182f462b317742`.
