# Agent prompt — SB09

Execute `SB09: Focused regression proof and user handoff` from the CanDoItAll Agent Chat UI Reuse Refactor Phase 1 bundle.

Outcome:

Run the final affected-scope and browser proof once, prepare the manual agent-chat regression checklist, and stop in an explicit awaiting-user-verification state.

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

- Do not mark ready-for-simple-chat-ui.
- Do not begin a new bundle automatically.
- Do not run broad tests by habit.
- Do not hide unrelated failures; classify them and record whether they block or reopen.

Stop when the subbundle and its checkpoint can be honestly closed, or record the concrete blocker. Do not continue into downstream Simple Chat UI work.
