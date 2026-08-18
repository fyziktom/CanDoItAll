# Codex execution prompt — SB10

Implement only **API security and external-client contract** on the current synchronized `simple-chats` branch.

## Goal

Harden the API for enterprise clients and future chatbot deployments without implementing channels, moderation, or UI.

## Success criteria

- [ ] An API client cannot choose or spoof stored conversation origin.
- [ ] Authorization-enabled hosts enforce distinct read, manage, and execute policies.
- [ ] Authorization-disabled trusted-local hosts preserve documented local behavior.
- [ ] No API/SSE error exposes prompts, system instructions, credentials, or raw provider failures.
- [ ] OpenAPI exposes versioned transport DTOs and stable links, not domain or EF entities.
- [ ] Future chatbot concerns remain a separate documented deployment boundary rather than dormant definition fields.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Transport-owned DTOs and policy authorization; future deployment is a separate aggregate/adapter boundary.
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
