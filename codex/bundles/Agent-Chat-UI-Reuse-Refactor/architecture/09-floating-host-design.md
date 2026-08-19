# Floating host design

## Neutral presentation seams

- floating window chrome/content composition when not already provided by BaseLib;
- catalog versus active-list presentation;
- participant list;
- active conversation card/list presentation;
- generic empty/loading/error states;
- generic active-chat lifecycle settings fields.

## Agent-owned behavior

- `IFloatingAgentChatCoordinator`;
- Agent workspace loading;
- handle creation and identity;
- prepared activation metadata;
- hide, reopen, close, and stop semantics;
- active limits and eviction;
- context registry and access;
- context affinity follow/detach;
- Agent history;
- conversation context;
- notifications and errors.

## Phase 1 UI invariant

Production labels and filters remain Agent-only. Do not add a mixed catalog or an Agents/Simple Chats filter. The neutral seam is proven through Agent projections only.

## Overlay proof

At the named desktop viewport inspect:

- closed state;
- catalog open;
- search results;
- active list;
- one visible chat;
- history dialog;
- agent switch dialog;
- relevant settings panel;
- context/affinity controls;
- close/hide/stop interactions;
- clipping, z-index, focus, and scroll ownership.
