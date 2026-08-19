# Acceptance evidence — SB06

For each criterion, provide behavioral/source evidence rather than only a test count.

- [x] All SB01-SB05 acceptance criteria have current-head proof.
- [x] No parallel legacy turn-execution or independent-transaction path remains reachable.
- [x] Focused backend Unit and PostgreSQL integration gates pass.
- [x] Migration/model and database-transfer proof pass when schema changed.
- [x] CP1 explicitly unlocks streaming work.

## Required semantic proof

- Intended case: execute the complete SB01-SB05 Unit and Integration owner union at one current head,
  including PostgreSQL transactions, transfer, profile, lease, detached request, and bounded reads.
- Negative/race/crash/failure case: prior CP1 head still exposes inline engine execution; cancellation,
  profile loss, transaction failure, lease contention, and query-bound cases remain in the unions.
- Why the old implementation would fail this proof: `d97e21c` still contains public engine
  `SendAsync`/private `SendCoreAsync`, bypassing durable dispatcher ownership.
- Exact source owner: application transaction/state/profile/lease/read contracts, EF adapters,
  dispatcher `LlmChatOperationExecutor`, and bounded MAF turn store.
- Exact command(s): final filtered Unit and Integration union commands, affected builds, EF pending-model
  command, current/historical source guards.
- Actual result: Unit 87/87; Integration 22/22; builds zero warnings/errors; model current; guards pass.
- Evidence artifact: `proof/SB06/` and `reviews/CP1-BACKEND-HARDENING.md`.
- Commit SHA: `a820b867fcf34cd07a93d201a9ffc492c243e647`.
