# Acceptance evidence — SB11

For each criterion, provide behavioral/source evidence rather than only a test count.

- [ ] Atomicity, profile fencing, distributed lease, cancellation, and idempotency scenarios pass against PostgreSQL.
- [ ] A slow streaming provider produces incremental SSE before terminal completion.
- [ ] Reconnect, gap, heartbeat, disconnect, explicit cancellation, and terminal closure pass through the real host.
- [ ] OpenAI/Azure/Ollama parser tests cover fragmented frames and failures without live network access.
- [ ] Migration, model snapshot, database transfer, and restart tests pass.
- [ ] Affected projects build with the CI package graph on the available Linux host.
- [ ] CP2 explicitly declares the backend/API ready or blocked.

## Required semantic proof

- Intended case:
- Negative/race/crash/failure case:
- Why the old implementation would fail this proof:
- Exact source owner:
- Exact command(s):
- Actual result:
- Evidence artifact:
- Commit SHA:
