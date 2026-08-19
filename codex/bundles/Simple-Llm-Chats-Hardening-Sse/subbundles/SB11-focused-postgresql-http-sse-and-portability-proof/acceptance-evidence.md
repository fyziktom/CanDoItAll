# Acceptance evidence — SB11

- [x] Atomicity, profile fencing, distributed lease, cancellation, and idempotency scenarios pass against PostgreSQL.
- [x] A slow streaming provider produces incremental SSE before terminal completion.
- [x] Reconnect, gap, heartbeat, disconnect, explicit cancellation, and terminal closure pass through the real host.
- [x] OpenAI/Azure/Ollama parser tests cover fragmented frames and failures without live network access.
- [x] Migration, model snapshot, database transfer, and restart tests pass.
- [x] Affected projects build with the CI package graph on the available Linux host.
- [x] CP2 explicitly declares the backend/API ready or blocked.

## Required semantic proof

- Intended case: fragmented real provider protocols produce neutral deltas; PostgreSQL owns atomic
  operation/event state; a real host returns 202, emits delta plus heartbeat while nonterminal, resumes
  from Last-Event-ID, and closes on one terminal event.
- Negative/race/crash/failure case: retry stops after a visible delta; transaction rollback emits no
  signal; profile/lease fencing blocks stale commit; disconnect does not cancel/redispatch; retained
  cursors gap; explicit cancel and provider failure remain redacted terminal outcomes.
- Why the old implementation would fail this proof: the reviewed implementation had completed-only
  provider calls, no durable event journal, synchronous turn completion, no replayable SSE, no exact
  LLM Chat authorization, and no server-owned HTTP provenance. The pre-SB11 proof also did not observe
  a heartbeat through the real endpoint and its source assertion failed under isolated artifacts.
- Exact source owner: provider wire drivers; `ProviderBackedLlmStreamingInvocationAdapter`; LLM Chats
  operation/event services; EF LLM Chat stores; Web LLM Chat API plus shared SSE writer.
- Exact command(s): three focused Linux test commands and two affected Linux builds recorded in
  `proof/SB11/transcripts`; no solution-wide or unfiltered lane.
- Actual result: provider/state/event semantics 105 passed with one fixture-only failure then exact 1/1
  repair; PostgreSQL/HTTP/SSE 43/43; Web package graph 0 warnings/errors; architecture/source guards pass.
- Evidence artifact: `proof/SB11/manifest.md`, `proof/SB11/semantic-invariants.md`, transcripts, and
  `reviews/CP2-STREAMING-API.md`.
- Commit SHA: `4ec4d2694d980d52936b4679ae676a0624d5c6fb`.

## Package-feed prerequisite

The Linux build used `UseLocalCanDoItAllLibraries=false`. A cold nuget.org-only restore first proved
that `CanDoItAll.FileTools.FileInteraction.Spreadsheet` 0.1.18 is unpublished. Packing the exact clean
sibling source into a container-only feed made the identical package-reference graph pass. SB13 must
either see that package on its configured feed or keep FINAL Not Ready; this prerequisite is not hidden
inside CP2's product/API decision.
