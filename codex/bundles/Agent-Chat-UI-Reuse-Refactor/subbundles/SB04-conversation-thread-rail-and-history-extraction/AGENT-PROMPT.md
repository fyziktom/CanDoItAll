# Agent prompt — SB04

Execute `SB04: Conversation thread rail and history extraction` from the CanDoItAll Agent Chat UI Reuse Refactor Phase 1 bundle.

Outcome:

Extract the reusable thread list, search, empty/loading states, item presentation, and bounded history dialog while leaving all agent session orchestration and approval semantics in the agent adapter.

Required posture:

- reconcile the live branch and current SharedInfo skills;
- read the subbundle README and prerequisites;
- inspect exact source, project references, CSS, consumers, and tests;
- make the smallest coherent change;
- preserve all current Agent behavior;
- keep backend effects in Agent-owned adapters;
- keep the neutral UI boundary free of Agent/LlmChats/runtime/persistence dependencies;
- use Components MCP before structural UI/CSS changes;
- use actual diff line ranges with impacted-test analysis;
- fail proof on zero or unexpected discovery;
- record proof and durable status before progression.

Do not:

- Do not change transcript or composer yet except minimal integration needed to keep compilation.
- Do not refactor Agent workspace persistence.
- Do not add Simple Chat conversation APIs.

Stop when the subbundle and its checkpoint can be honestly closed, or record the concrete blocker. Do not continue into downstream Simple Chat UI work.
