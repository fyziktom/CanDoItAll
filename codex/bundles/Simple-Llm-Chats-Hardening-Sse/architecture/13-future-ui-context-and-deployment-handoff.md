# Future handoff

FINAL Ready unlocks preparation—not implementation—of:

1. shared chat-component isolation;
2. Simple Chat UI/floating catalog integration;
3. explicit Project Structure/manual/file context inputs;
4. enterprise/public chatbot deployments.

## Backend contracts the future UI may rely on

- immutable definition summary/revision;
- paged conversation/thread list and transcript page;
- 202 turn admission;
- operation status and typed terminal outcome;
- replayable SSE events;
- explicit cancellation;
- stable origin/capability flags;
- no agent approval/tool/runtime semantics.

## Backend contracts not created here

- common `ChatTarget` UI model;
- Project Structure selection/subtree snapshots;
- attachments/voice;
- chatbot participants/channel adapters;
- UI window lifecycle.

The UI bundle must not reuse product domain entities directly as view state.
