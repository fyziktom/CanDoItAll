# Deferred work and later bundle boundaries

## Bundle 2 — shared chat-component isolation

Goals:

- extract neutral transcript/header/composer/thread-list components from agent UI;
- retain agent approvals, execution stream, runtime details, and tools in agent-only components;
- introduce neutral chat target identity and capability projection;
- no simple-chat product integration yet.

## Bundle 3 — simple-chat UI integration

Goals:

- definition administration UI;
- simple-chat thread UI;
- floating catalog `All / Agents / Chats`;
- shared active-chat shell;
- API/application service integration;
- no Project Structure context until its backend source is ready.

## Bundle 4 — contextual input

Goals:

- `IProjectStructureLlmChatContextSource` in Workbench;
- whole project, selected node, selected nodes, and subtree capture;
- preview, coverage, omissions, token estimate, fingerprint, profile generation;
- next-turn and pinned context lifecycle;
- untrusted-data prompt envelope;
- optional attachments.

## Bundle 5 — enterprise chatbot deployments

Goals:

- deployment aggregate and adapters;
- anonymous/external participants;
- moderation and rate limiting;
- channel-specific auth and retention;
- streaming and asynchronous dispatch where required.

These bundles may change after evidence, but their responsibilities must not be pulled into the current
backend/API bundle without explicit change control.
