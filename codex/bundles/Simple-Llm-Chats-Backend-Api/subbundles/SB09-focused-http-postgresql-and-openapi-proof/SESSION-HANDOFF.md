# SB09 session handoff

Status: Completed

## Baseline

- starting commit: `c0117109c6ef6166d1d8b1b42d75e7f4af83c5ee` plus completed SB00-SB08 working tree
- ending commit/working-tree state: working tree after SB09; no commit created
- executor/session: Codex bundle workflow
- date: 2026-08-14

## Work completed

- Added one coherent real-host PostgreSQL API proof for the complete definition, conversation, turn,
  operation, cancellation, recovery, paging, lifecycle, and OpenAPI surface.
- Proved one provider with two models exposing different thinking-effort sets, provider default,
  explicit None, supported High, unsupported override rejection, revision persistence, dispatch, and
  requested/effective audit.
- Proved idempotent response replay and unchanged message/invocation counts, sanitized provider failure,
  runtime-profile fencing, complete transfer round trip, and previous-schema migration.
- Fixed PostgreSQL timestamp-precision defects in transcript delta comparison and first/replay response
  canonicalization; added a typed, logged unexpected-failure safety boundary.
- Completed CodeAnalytics review and CP2.

## Files changed

- focused PostgreSQL API integration proof and deterministic external provider boundary
- EF conversation store and generic conversation completion canonicalization
- durable operation evidence canonicalization and unexpected-failure handling
- operation-service logging dependency and unit harness wiring
- CP2 review, SB09 proof manifest, transcripts, and handoff

## Validation executed

| Command | Result | Duration/notes |
|---|---|---|
| Focused real-host API run | Failed as expected during repair | Exposed timestamp precision delta mismatch. |
| Focused replay run | Failed as expected during repair | Exposed first/replay timestamp instability. |
| Focused full scenario run | Reached final graph assertion | All behavior passed; test omitted canonical system message from expected count. |
| Combined focused CP2 command | Pass, 3/3 | Real-host PostgreSQL API, transfer round trip, and previous-schema migration. |
| Source boundary audit | Pass | Separate routes, no secret/persistence/agent dependency, no Razor changes. |
| CodeAnalytics snapshot | Pass | Four projects; zero cycles, diagnostics, blocking errors, or questions. |

## Architecture assertions

- The primary behavior is driven only through HTTP; direct EF access is limited to postcondition and
  audit assertions.
- Only the external provider-profile source and stateless invocation port are deterministic test
  boundaries; the canonical resolver, application services, Web endpoints, EF stores, and PostgreSQL
  composition are real.
- Provider/model effort capability remains sourced from the canonical provider runtime contracts.
- PostgreSQL-committed state is the canonical first-response and replay representation.
- CodeAnalytics snapshot `snap-20260814191734-4c429922` is cycle- and diagnostic-free.

## Bugs found and fixed

- Corrected transcript delta equality to account for PostgreSQL microsecond precision without weakening
  the append/remove/no-change contract.
- Reloaded committed transcript and operation evidence so idempotent replay is byte-stable.
- Added sanitized durable handling for unexpected operation-send failures.
- Corrected the proof's graph count to include the canonical system-prompt entry.

## Deviations

- The test host replaces the live external provider boundary with a deterministic provider profile and
  invocation port; live external provider calls are explicitly forbidden by SB09.

## Residual risks and known gaps

- The repository-wide stable Release gate and pending-model check remain owned by SB11.
- Two CodeAnalytics file-size warnings are non-blocking and documented in the CP2 review.

## Next gate

- next subbundle/checkpoint: SB10 — documentation, boundary guards, and cleanup
- unlock decision: CP2 passed; SB10 unlocked after bundle validators
