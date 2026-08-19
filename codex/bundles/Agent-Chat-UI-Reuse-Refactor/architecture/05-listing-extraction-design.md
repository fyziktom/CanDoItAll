# Listing extraction design

## Extract

- participant identity/avatar/card shell;
- compact list and item;
- generic selected/busy/disabled state;
- generic tags, badges, metadata, details tooltip;
- action slots for select, double-click, favorite, new chat, history, and product-specific actions;
- reusable picker search/tag filtering only when the data is already a neutral projection.

## Keep agent-owned

- team tree and team membership;
- agent lifecycle status and workload meaning;
- capability counts and descriptions;
- provider privacy calculation;
- `AgentSpecialTags` and favorite persistence;
- managed agent identities and their specialized actions;
- exact rules that determine whether an Agent can open a chat;
- Agent catalog loading and service calls.

## Compatibility

`AgentSelectionCard`, `AgentCompactList`, `AgentCompactListItem`, and `AgentSwitchDialog` may remain as public facades that map `AgentDefinition` to neutral presentation models.

Do not change all upstream callers to neutral records in one step. First prove the neutral owner, then adapt each facade, then migrate selected callers only when it reduces coupling.

## Test intent

- neutral card/list rendering and callbacks;
- opaque non-Guid keys;
- optional badges/tags/meta/actions;
- selected/busy/disabled accessibility;
- agent facade mapping;
- current agent ordering/filter/favorite semantics;
- current test ids and accessible names;
- double-click and action propagation.
