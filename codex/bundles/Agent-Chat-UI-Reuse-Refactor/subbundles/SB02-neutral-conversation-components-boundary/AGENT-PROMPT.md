# Agent prompt — SB02

Execute `SB02: Neutral Conversation Components boundary` from the CanDoItAll Agent Chat UI Reuse Refactor Phase 1 bundle.

Outcome:

Create the app-owned, backend-neutral Razor boundary, its presentation contracts, isolated tests, project references, and source/dependency guards without migrating existing production consumers yet.

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

- Do not reference Modules.LlmChats.
- Do not add DI registrations or runtime services.
- Do not migrate Agent components.
- Do not create a universal conversation service.
- Do not add a new product menu/route.

Stop when the subbundle and its checkpoint can be honestly closed, or record the concrete blocker. Do not continue into downstream Simple Chat UI work.
