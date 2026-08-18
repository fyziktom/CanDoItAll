# Agent prompt — SB06

Execute `SB06: Definition settings and editor-surface extraction` from the CanDoItAll Agent Chat UI Reuse Refactor Phase 1 bundle.

Outcome:

Extract reusable editor shell, identity/avatar/instructions fields, provider/model selection presentation, and optional advanced-setting slots without moving agent-only runtime policy or binding to Simple Chat domain types.

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

- Do not bind the neutral editor to LlmChatDefinition.
- Do not move Agent persistence or validation into the neutral project.
- Do not remove or genericize Agent-only policy tabs.
- Do not add Simple Chat settings pages.

Stop when the subbundle and its checkpoint can be honestly closed, or record the concrete blocker. Do not continue into downstream Simple Chat UI work.
