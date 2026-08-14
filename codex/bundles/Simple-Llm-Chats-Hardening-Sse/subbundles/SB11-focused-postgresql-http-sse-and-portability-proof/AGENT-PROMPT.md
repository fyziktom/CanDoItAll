# Codex execution prompt — SB11

Implement only **Focused PostgreSQL, HTTP, SSE, and portability proof** on the current synchronized `simple-chats` branch.

## Goal

Prove the complete hardened backend and streaming API through realistic deterministic paths before cleanup and the one-time broad gate.

## Success criteria

- [ ] Atomicity, profile fencing, distributed lease, cancellation, and idempotency scenarios pass against PostgreSQL.
- [ ] A slow streaming provider produces incremental SSE before terminal completion.
- [ ] Reconnect, gap, heartbeat, disconnect, explicit cancellation, and terminal closure pass through the real host.
- [ ] OpenAI/Azure/Ollama parser tests cover fragmented frames and failures without live network access.
- [ ] Migration, model snapshot, database transfer, and restart tests pass.
- [ ] Affected projects build with the CI package graph on the available Linux host.
- [ ] CP2 explicitly declares the backend/API ready or blocked.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Behavioral proof through real HTTP host and PostgreSQL; fake provider replaces only the external network boundary.
- Do not add service-location, an ambient transaction, fake asynchronous fire-and-forget work, or a
  final partial-class extension.
- Run only focused validation allowed by the subbundle.
- Record exact commands, results, host, database/dependency mode and commit SHA.
- Stop on a cycle, contradiction, untrusted prerequisite, or missing required proof.
- Do not continue into the next subbundle in the same execution unless the progression record explicitly
  authorizes it.

## Required output

- coherent production/test changes for this outcome;
- updated proof manifest and handoff;
- architecture/source evidence;
- progression decision;
- honest blocker when a criterion cannot be proven.
