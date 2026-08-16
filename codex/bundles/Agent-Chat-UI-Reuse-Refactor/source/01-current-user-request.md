# Current user request

The current Simple Chats backend phase is complete. UI work must begin cautiously.

Phase 1 must only refactor existing chat-related UI so that reusable components are isolated, direct UI coupling to the Agent backend is reduced, and later Simple Chat UI can reuse selected surfaces.

The phase includes:

- current Agent Chat workspace components;
- current participant/agent listing and picker components;
- current Agent settings/editor surfaces;
- floating Agent Chat catalog and lifecycle settings presentation.

The phase must not yet use the extracted components for Simple Chats. After implementation, the user will manually verify that Agent Chats behave exactly as before. A separate later bundle will integrate Simple Chats.

Future product vision, intentionally deferred here:

- Simple Chats have a name, avatar, system prompt, provider/model, temperature, and related settings;
- Agents and Simple Chats may later appear in a shared floating catalog with explicit filters;
- Simple Chats do not receive agent tools or skills;
- a future Simple Chat may expose an Add context action for project structure or selected node/subtree context;
- backend streaming/SSE is available for a later UI integration phase.

The bundle must follow the current CanDoItAll.SharedInfo impacted-test standards instead of repeatedly running all tests.
