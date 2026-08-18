# Codex execution prompt — SB07

Implement only **Provider-neutral streaming contracts and drivers** on the current synchronized `simple-chats` branch.

## Goal

Add true provider-neutral incremental output without coupling Simple Chats to concrete provider SDKs or breaking existing complete-response callers.

## Success criteria

- [ ] Existing ILlmInvocationPort callers remain source- and behavior-compatible.
- [ ] OpenAI, Azure OpenAI, and Ollama produce incremental text through one provider-neutral contract.
- [ ] A non-incremental supported driver uses a deterministic single-delta fallback or typed unsupported result.
- [ ] No automatic retry occurs after the first emitted delta.
- [ ] Every actual provider dispatch attempt receives a distinct monotonic audit ordinal and deterministic outcome.
- [ ] Streaming failures expose no credentials, raw frames, or raw provider errors.

## Constraints

- Read the root execution contract, this README, owned requirements, architecture records, and test
  budget first.
- Reinspect the current source; do not rely on reviewed SHAs after SB00.
- Preserve Simple Chat separation from agents, tools, skills, MCP, memory, processes, Razor and UI.
- Use the selected pattern: Optional provider capability plus provider-neutral adapter; existing non-streaming port remains compatible.
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
