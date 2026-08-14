# Codex execution prompt — SB03

Implement only **Whole-use-case profile fencing** on the current synchronized `simple-chats` branch.

## Goal

Fence every Simple Chat command and query from its first database read through its final commit/return against one database profile identity and generation.

## Success criteria

- [ ] Every public LLM Chat application operation captures profile identity before its first read.
- [ ] All repositories, provider resolution, transcript commands, and audit writes use the captured operation scope.
- [ ] A profile switch prevents every subsequent old-generation durable commit.
- [ ] A switch during provider execution yields deterministic non-success or RecoveryRequired with retained usage evidence.
- [ ] No current-profile DbContext or provider lease is cached across operations.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Explicit operation scope/lease; no captured current-profile state across operations and no ambient lookup after admission.
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
