# Agent prompt — SB01

Execute `SB01: Baseline, architecture inventory, and UI parity contract` from the CanDoItAll Agent Chat UI Reuse Refactor Phase 1 bundle.

Outcome:

Freeze the real branch baseline, build scoped architecture evidence, inventory all agent-chat consumers, and capture the current rendered behavior before production refactoring begins.

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

- Do not create the neutral project yet.
- Do not change Razor markup or CSS.
- Do not begin opportunistic cleanup.
- Do not treat a missing symbol as evidence until snapshot health is proven.

Stop when the subbundle and its checkpoint can be honestly closed, or record the concrete blocker. Do not continue into downstream Simple Chat UI work.
