# Agent prompt — SB08

Execute `SB08: Agent consumer migration and architecture closure` from the CanDoItAll Agent Chat UI Reuse Refactor Phase 1 bundle.

Outcome:

Migrate every existing agent consumer through the neutral presentation boundary, remove superseded duplication, prove dependency direction, and close architecture review without activating Simple Chats.

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

- Do not bypass the neutral project by copying markup into each consumer.
- Do not let Processes reference Modules.AgentFramework UI directly when a proper facade already exists.
- Do not remove compatibility APIs still used by a live consumer.
- Do not add Simple Chat UI while consolidating consumers.

Stop when the subbundle and its checkpoint can be honestly closed, or record the concrete blocker. Do not continue into downstream Simple Chat UI work.
