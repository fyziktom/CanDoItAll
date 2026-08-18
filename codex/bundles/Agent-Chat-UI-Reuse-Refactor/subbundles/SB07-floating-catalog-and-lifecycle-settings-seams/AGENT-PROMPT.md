# Agent prompt — SB07

Execute `SB07: Floating catalog and lifecycle-settings seams` from the CanDoItAll Agent Chat UI Reuse Refactor Phase 1 bundle.

Outcome:

Extract floating-window presentation, catalog/list composition, and generic active-chat lifecycle fields while keeping the current host agent-only and preserving context, handle, retention, and preparation behavior.

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

- Do not rename current product labels to imply Simple Chats.
- Do not create a multi-source coordinator.
- Do not move context or handle lifecycle into the neutral project.
- Do not add project-structure context capture.

Stop when the subbundle and its checkpoint can be honestly closed, or record the concrete blocker. Do not continue into downstream Simple Chat UI work.
