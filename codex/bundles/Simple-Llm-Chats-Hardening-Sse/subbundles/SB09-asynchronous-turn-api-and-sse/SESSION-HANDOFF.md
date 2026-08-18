# Session handoff — SB09

State: **Ready**

## Entry checklist

- [x] Root bundle status read
- [x] Dependencies complete and proof trusted
- [x] Actual repository/branch/head recorded
- [x] Current source and nearby tests inspected
- [x] Test budget understood
- [x] Database/dependency mode recorded

## Work performed

- Changed retry-safe turn admission to always return 202 with Location, stable operation metadata,
  replay disposition, revisions, latest event sequence, and status/events/cancel links.
- Added a product-owned profile-fenced stream session that pages the durable PostgreSQL journal and
  uses the existing local signal only for bounded latency.
- Added `GET /api/llm-chat-operations/{operationId}/events` with Last-Event-ID/after replay, explicit
  gap recovery, typed versioned envelopes, terminal closure, and stable invalid-cursor errors.
- Extended the shared SSE writer for per-item event names and terminal predicates while preserving its
  existing cursor, heartbeat, anti-buffering, disconnect, and profile-lifetime behavior.
- Kept execution detached from both POST and SSE request lifetimes; only the existing cancel endpoint
  mutates cancellation state.
- Added direct transport and real PostgreSQL proof for reconnect, gap, disconnect, cancellation,
  failure, profile switch, terminal closure, OpenAPI, and sensitive-data exclusion.

## Commands and results

- expected-red PostgreSQL SSE test: failed on missing `replayed` metadata before implementation;
- affected Web build: passed with 0 warnings/errors;
- focused 22-case LlmChats/API transport union: 20 passed, with two test-only assertion/fixture defects;
- exact corrected pair: 2/2 passed, giving a compositional current-head aggregate of 22/22;
- CodeAnalytics `snap-20260815064713-4eb8c3ec`: three scoped projects, zero cycles, diagnostics, or
  open questions;
- source guards: no product Web dependency, projection execution ownership, sensitive SSE contract
  fields, or production partial expansion.

Exact commands and results are recorded in `proof/SB09/transcripts` and `proof-manifest.json`.

## Bugs discovered and resolved

- The initial writer integration serialized an internal wrapper as SSE data. The stream now serializes
  only the public versioned event envelope; terminal metadata is ignored by JSON.
- Session startup could leak an acquired runtime lease if cancellation or an unexpected failure occurred
  after acquisition. All exceptional exits now dispose it.
- The previous POST contract still exposed synchronous 200/terminal behavior. Successful admission and
  exact replay now consistently return 202 with explicit replay metadata.

## Deviations

- Six filtered test attempts exceeded the four-command budget by two. One was a sandbox-only
  control-plane denial, one was the required expected-red run, two stopped at compile on missing test
  namespaces, one ran 22 cases, and one reran only the corrected pair. No broad test lane ran.
- The direct product-owner assertion was consolidated into the real PostgreSQL profile-switch test after
  the separate Unit attempt stopped at compile; the discarded Unit test is not committed.

## Acceptance result

- [x] 202 admission is prompt and retry-explicit.
- [x] Durable ordered replay, reconnect, gap, and one-call semantics pass.
- [x] Disconnect is observational; explicit cancel remains authoritative.
- [x] Success, failure, cancellation, and RecoveryRequired terminal projections close.
- [x] Shared cursor/heartbeat/anti-buffering/profile behavior is retained.
- [x] Prompt, credential, provider endpoint, and raw provider failure data do not leak.

## Architecture result

- [x] Owner moved or strengthened as planned
- [x] Old shallow path removed/unreachable
- [x] Direct tests target the new owner
- [x] No forbidden reference/cycle/partial expansion
- [x] Architecture record updated if design changed

## Progression

Ready. SB10 is unlocked to add bearer-scope enforcement, server-owned origin, and the hardened external
client contract without changing the durable SSE ownership established here.
