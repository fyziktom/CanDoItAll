# Codex execution prompt — SB02

Implement only **Atomic turn state machine and recovery** on the current synchronized `simple-chats` branch.

## Goal

Make admission, finalization, cancellation, compensation, reconciliation, and idempotent replay one deterministic durable protocol.

## Success criteria

- [ ] Turn admission is one transaction across operation, transcript, evidence, and event state.
- [ ] Successful completion is one transaction across assistant message, usage, active-turn clearing, and operation success.
- [ ] Failed compensation cannot leave a terminal Failed or Cancelled operation with a live active turn.
- [ ] A cancellation request committed before finalization prevents Succeeded.
- [ ] Same operation ID and fingerprint replays the original result even after later lifecycle changes.
- [ ] Same operation ID with a different fingerprint conflicts before provider dispatch.
- [ ] Conversation archive cannot race an active or nonterminal turn.
- [ ] Direct completion and recovery reduce identical durable evidence to the same outcome.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Transactional state-machine command store plus pure deterministic reducer; provider calls stay outside transactions.
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
