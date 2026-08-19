# Codex execution prompt — SB08

Implement only **Durable stream event journal and pipeline** on the current synchronized `simple-chats` branch.

## Goal

Integrate incremental output into the durable operation lifecycle with replayable, bounded, non-canonical events.

## Success criteria

- [ ] Every operation event has a unique monotonic sequence within its operation.
- [ ] State-transition events commit in the same transaction as their state.
- [ ] Text chunks are coalesced and bounded rather than one row per token.
- [ ] Partial output is replayable but never canonical unless finalization succeeds.
- [ ] A second instance reads all committed events without first-instance memory.
- [ ] Event payloads contain no system prompt, user prompt, credential, or raw provider error.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Transactional outbox/event journal with post-commit local signal; journal is replay evidence, transcript stays canonical.
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
