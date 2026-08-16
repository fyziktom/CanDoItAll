# User Request

Codex completed the first UI reuse refactor and pushed it to the `simple-chats` branch.

The requested work is to inspect how Codex implemented it, decide whether the components are prepared correctly for safe Simple Chat UI integration, and prepare the appropriate follow-up bundle. The user manually tested changing Agent settings and then chatting with an Agent around Project Structure; it behaved as before.

Decision rule requested by the user:

- when cleanup is still necessary, put it first;
- when the boundary is ready, continue with Simple Chat UI integration;
- the bundle may contain multiple phases, with cleanup before integration.

Original product intent retained:

- Simple Chats should feel close to Agents in ordinary use;
- definitions have name, avatar, system prompt/basic behavior, temperature, provider/model settings;
- Simple Chats appear beside Agents in floating chat discovery with a type filter;
- Simple Chats have no tools or skills;
- Project Structure or selected-node/subtree context is a later explicit action rather than ambient authority.

The bundle must follow the current SharedInfo focused-test standards and must not repeatedly run all test suites.
