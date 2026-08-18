# Floating Conversation Shell

## Neutral Shell Contract

`CanDoItAll.Conversations.Shell` owns:

- catalog visibility and overlay window geometry;
- `All / Agents / Chats` kind filter;
- `Available / Active` lifecycle tabs;
- merge/order/search of source-neutral participant and active-item projections;
- dispatch of declared action keys to the source contributor;
- rendering of source-owned focused-window descriptors through `DynamicComponent`.

## Contributor Contract

Each contributor provides:

- stable source id and participant kind;
- initialization/lifetime and `Changed` notification;
- available participant snapshot;
- active conversation/window snapshot;
- participant/active action handlers;
- focused window descriptors;
- optional source status badges.

## Agent Contributor

Retains:

- current-context access filtering and fail-closed loading/error states;
- affinity follow/detach and context badges;
- thread history;
- Agent coordinator retention, prepared metadata, close decision, and stop behavior;
- focused `AgentChatPanel`.

## Simple Chat Contributor

Uses:

- Active definitions as available participants;
- durable conversations as history;
- UI-local/window lifecycle over durable conversation ids;
- operation streaming/cancel/recovery from the same application state as the main page;
- no automatic ambient context.

## Action Semantics

- Close/hide window: UI lifecycle only.
- Stop Agent: Agent handle lifecycle.
- Cancel response: durable Simple Chat operation command.
- Archive conversation: durable conversation command.

These actions must never share one ambiguous `Stop` callback.
