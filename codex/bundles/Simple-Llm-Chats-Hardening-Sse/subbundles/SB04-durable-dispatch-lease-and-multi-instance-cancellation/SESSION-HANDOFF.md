# Session handoff — SB04

State: **Completed — SB05 unlocked**

## Entry checklist

- [x] Root bundle status read
- [x] Dependencies complete and proof trusted
- [x] Actual repository/branch/head recorded
- [x] Current source and nearby tests inspected
- [x] Test budget understood
- [x] Database/dependency mode recorded

## Work performed

Implemented a database-backed competing-consumer lease with immutable owner/epoch identity, bounded
heartbeat/expiry, durable cancellation observation, fail-closed post-dispatch recovery, and a hosted
dispatcher detached from HTTP request lifetime. Admission is atomic and returns queued work only when
an executor is registered. A local signal and CTS registry are latency optimizations; durable polling
and database state remain authoritative.

## Files changed

Product operation state/transitions, application lease/dispatcher/executor owners, persistence rows,
EF repository and heartbeat adapter, host composition, API error mapping, MAF admitted-turn resume,
PostgreSQL migration/snapshot/transfer schema, and focused Unit/Integration tests. See
`proof/SB04/manifest.md` for the artifact inventory.

## Commands and results

- Historical pre-SB04 request-lifetime regression at `da8cfeb8aa08917350b2433c377a8d6c6abc66dc`:
  exit 1, 0 passed/1 failed, 0 skipped, expected `TimeoutException` while HTTP still owned provider execution.
- Focused Unit `FullyQualifiedName~LlmChatOperation`: exit 0, 15 passed/0 failed/0 skipped.
- Focused PostgreSQL `LlmChatOperationDispatchClaimIntegrationTests`: exit 0, 2 passed/0 failed/0 skipped.
- Focused real-host request-disconnect API test: exit 0, 1 passed/0 failed/0 skipped.
- Affected Unit build: exit 0, 0 warnings/0 errors. LLM Chats no-dependencies compile after the final
  logging correction: exit 0, 0 warnings/0 errors.
- EF pending-model check: exit 0, no pending changes.
- `git diff --check`: exit 0.
- CodeAnalytics `snap-20260815030209-a236038a`: 4 projects, 0 cycles, 0 diagnostics, no blocking errors.

## Bugs discovered and resolved

- The old inline HTTP execution path caused the historical request-admission timeout; execution now
  resumes from the exact admitted turn in the dispatcher.
- A first API run used stale compiled output and returned 500; the affected Integration project was
  rebuilt and the unchanged test then passed.
- The first final build was denied writes to the configured sibling Components repository; the
  unchanged command passed outside the sandbox with zero warnings/errors.
- An empty runtime-profile-change catch in cancellation reduction was replaced with actionable,
  non-sensitive lease identity logging.

## Deviations

The focused build budget was exceeded by diagnostic no-dependencies/source-refresh builds after
compile corrections and sandbox-only failures. No broad Unit, Integration, or solution test command
was run. Historical proof setup required a shorter detached worktree because Windows MAX_PATH blocked
the first nested path; both temporary worktrees and junctions were removed after proof.

## Acceptance result

- [x] Only one instance can hold an execution lease for an operation at a time.
- [x] A client disconnect after admission does not cancel the durable operation.
- [x] Explicit cancellation reaches a local owner and is observed cross-instance within the configured bound.
- [x] Local registry absence never recovers or abandons another instance's live operation.
- [x] Expired pre-dispatch work may be reclaimed, while expired post-dispatch work becomes RecoveryRequired.
- [x] A host without an available dispatcher cannot falsely accept unexecutable work.

## Architecture result

- [x] Owner moved or strengthened as planned
- [x] Old shallow path removed/unreachable
- [x] Direct tests target the new owner
- [x] No forbidden reference/cycle/partial expansion
- [x] Architecture record updated; planned ADR-H05 is proven without deviation

## Progression

Ready. SB04 is complete at `7389daff6c21a4568895e514debe110434908d67`; SB05 is unlocked.
