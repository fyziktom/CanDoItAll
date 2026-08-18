# Codex execution prompt — SB05

Implement only **Bounded transcript queries and pagination** on the current synchronized `simple-chats` branch.

## Goal

Remove full-transcript and N+1 reads from list, detail, pagination, and per-turn context-window construction.

## Success criteria

- [ ] Transcript paging executes a bounded SQL query and never materializes the full transcript.
- [ ] Conversation and definition listings do not issue one query per item.
- [ ] Context-window construction reads only the bounded entries it can send.
- [ ] Externally exposed collections use deterministic cursors and enforced page limits.
- [ ] Large-transcript tests prove stable memory/query behavior without changing canonical content.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: CQRS-style bounded read models over the canonical tables; no second persistence truth.
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
