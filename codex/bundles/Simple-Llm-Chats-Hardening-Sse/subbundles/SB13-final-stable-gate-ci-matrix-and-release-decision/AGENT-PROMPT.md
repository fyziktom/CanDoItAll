# Codex execution prompt — SB13

Implement only **Final stable gate, CI matrix, and release decision** on the current synchronized `simple-chats` branch.

## Goal

Run expensive repository-wide evidence exactly once at the final head and decide whether merge and later UI-isolation work are unlocked.

## Success criteria

- [ ] The final Release solution build passes at the exact recorded commit.
- [ ] The repository stable filtered test gate passes at the exact recorded commit.
- [ ] Documentation and pending-model-change checks pass.
- [ ] Windows, Linux, and macOS CI jobs pass for the same commit.
- [ ] No broad suite was rerun after an unchanged failure merely to seek a different result.
- [ ] FINAL explicitly states whether UI/component-isolation work is unlocked.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Release gate only; any source change reopens its owner and invalidates final evidence.
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
