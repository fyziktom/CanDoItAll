# Codex execution prompt — SB06

Implement only **Backend hardening checkpoint** on the current synchronized `simple-chats` branch.

## Goal

Prove the non-streaming backend is transactionally, lifecycle, profile, and multi-instance safe before adding streaming complexity.

## Success criteria

- [ ] All SB01-SB05 acceptance criteria have current-head proof.
- [ ] No parallel legacy turn-execution or independent-transaction path remains reachable.
- [ ] Focused backend Unit and PostgreSQL integration gates pass.
- [ ] Migration/model and database-transfer proof pass when schema changed.
- [ ] CP1 explicitly unlocks or blocks streaming work.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Checkpoint only; no new abstraction unless required to remove a duplicate production path.
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
