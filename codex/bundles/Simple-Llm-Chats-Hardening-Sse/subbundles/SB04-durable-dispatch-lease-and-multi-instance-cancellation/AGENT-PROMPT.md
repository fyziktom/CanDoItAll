# Codex execution prompt — SB04

Implement only **Durable dispatch lease and multi-instance cancellation** on the current synchronized `simple-chats` branch.

## Goal

Decouple paid execution from the HTTP request and make ownership, heartbeat, cancellation, and stale-run recovery safe across application instances.

## Success criteria

- [ ] Only one instance can hold an execution lease for an operation at a time.
- [ ] A client disconnect after admission does not cancel the durable operation.
- [ ] Explicit cancellation reaches a local owner and is observed cross-instance within the configured bound.
- [ ] Local registry absence never recovers or abandons another instance's live operation.
- [ ] Expired pre-dispatch work may be reclaimed, while expired post-dispatch work becomes RecoveryRequired.
- [ ] A host without an available dispatcher cannot falsely accept unexecutable work.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Database-backed competing-consumer lease with local wake-up; at-most-one owner and fail-closed uncertain dispatch.
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
