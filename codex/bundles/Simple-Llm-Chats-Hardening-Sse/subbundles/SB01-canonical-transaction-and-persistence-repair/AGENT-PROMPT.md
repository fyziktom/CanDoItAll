# Codex execution prompt — SB01

Implement only **Canonical transaction and persistence repair** on the current synchronized `simple-chats` branch.

## Goal

Remove duplicate writable conversation truth and make definition/conversation/transcript mutations genuinely atomic in one database transaction.

## Success criteria

- [ ] Conversation title and transcript metadata have exactly one canonical writable owner.
- [ ] Conversation creation commits product binding and transcript root together or commits neither.
- [ ] Conversation rename updates the canonical title once and cannot leave divergent rows.
- [ ] No production conversation store creates a second AppDbContext inside an active product command.
- [ ] Migration and transfer payloads preserve the repaired canonical model.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Single transactional command store with separate read models; no ambient or service-located transaction.
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
