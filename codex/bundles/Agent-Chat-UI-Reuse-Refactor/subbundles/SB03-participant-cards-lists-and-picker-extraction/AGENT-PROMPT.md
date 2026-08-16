# Agent prompt — SB03

Execute `SB03: Participant cards, compact lists, and picker extraction` from the CanDoItAll Agent Chat UI Reuse Refactor Phase 1 bundle.

Outcome:

Extract reusable participant presentation surfaces while keeping agent ordering, favorites, team semantics, actions, copy, and test selectors behaviorally unchanged through compatibility adapters.

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

- Do not add Simple Chat cards or filters.
- Do not generalize Agent team/capability policy into the neutral contract.
- Do not move Agent services into the neutral project.
- Do not remove compatibility facades before all consumers are proven.

Stop when the subbundle and its checkpoint can be honestly closed, or record the concrete blocker. Do not continue into downstream Simple Chat UI work.
