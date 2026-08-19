# Agent prompt — SB05

Execute `SB05: Conversation workspace, transcript, and composer extraction` from the CanDoItAll Agent Chat UI Reuse Refactor Phase 1 bundle.

Outcome:

Extract safe markdown, message/transcript presentation, composer chrome, and extension slots while retaining execution, approval, voice, attachment, prompt-gallery, and backend behavior in the legacy agent facade.

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

- Do not add SSE/polling/API clients.
- Do not move send/cancel/approval commands into neutral UI.
- Do not remove voice/attachments/prompt/runtime behavior.
- Do not cache transcript content in a way that blocks parameter updates.
- Do not create one universal workspace with a broad boolean matrix.

Stop when the subbundle and its checkpoint can be honestly closed, or record the concrete blocker. Do not continue into downstream Simple Chat UI work.
