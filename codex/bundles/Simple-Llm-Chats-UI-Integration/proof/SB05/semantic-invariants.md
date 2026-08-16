# SB05 Semantic Invariants

## Preserved Agent behavior

- Agent settings remain a full-viewport editor with every existing configuration tab and visible Clear/Save actions.
- Main Agent chat continues to render the durable transcript, approval state, and composer through its existing owner.
- Floating Agent chat continues to create a durable thread, supports explicit Keep active/Stop decisions, and reopens the same retained active chat.
- Contextual launch remains affinity-aware: Project Structure reports the Garden context and filters the catalog through the existing access policy.

## Activation fence

- No Simple Chat route or navigation/catalog item exists before CP1.
- `/chats` returns HTTP 404 at the CP1 browser gate.
- SB05 changes no production source; it only decides whether the already prepared boundaries are safe to consume.

## Architecture

- `CanDoItAll.Conversations.Components` remains backend-neutral and has no project references.
- No project reference or dependency cycle is introduced at CP1.
- The two previously recorded AgentFramework module/type cycles are unchanged and outside the SB02-SB04 change set.
