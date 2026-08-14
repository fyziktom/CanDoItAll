# Codex execution prompt — SB12

Implement only **Documentation, guards, and dead-path cleanup** on the current synchronized `simple-chats` branch.

## Goal

Remove superseded paths, update authoritative documentation, and install guards that keep Simple Chats independent from agents and UI.

## Success criteria

- [ ] No production path uses the independent-context UoW or synchronous request-owned provider execution.
- [ ] No Razor, floating-chat, shared-component, Project Structure context, or UI integration was added.
- [ ] Executable guards enforce dependency direction and prevent agent/tool/skill/MCP leakage.
- [ ] Authoritative docs accurately describe asynchronous operation and SSE contracts.
- [ ] Future UI, context, and enterprise deployment bundles have explicit ownership handoffs.
- [ ] All proof and closure records reference the actual implementation head.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Documentation plus executable architecture guards; no new runtime layer.
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
