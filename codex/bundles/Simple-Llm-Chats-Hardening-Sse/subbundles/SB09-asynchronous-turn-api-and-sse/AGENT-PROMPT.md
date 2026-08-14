# Codex execution prompt — SB09

Implement only **Asynchronous turn API and SSE** on the current synchronized `simple-chats` branch.

## Goal

Expose durable asynchronous turn admission and replayable SSE for slow/long responses without binding execution to one HTTP connection.

## Success criteria

- [ ] Turn start returns 202 without waiting for provider completion.
- [ ] SSE delivers ordered deltas and exactly one terminal operation event.
- [ ] Reconnect resumes without duplicate semantic text or a second provider call.
- [ ] A replay gap emits stream.gap with a usable recovery cursor while status remains authoritative.
- [ ] SSE disconnect does not cancel or abandon the operation.
- [ ] Explicit cancellation is visible in operation status and event stream.
- [ ] The stream closes after terminal success, failure, cancellation, or RecoveryRequired.
- [ ] Existing anti-buffering, heartbeat, cursor, and profile-lifetime behavior is reused.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: 202 command resource plus GET status and GET durable replay stream; SSE is a projection, never execution owner.
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
