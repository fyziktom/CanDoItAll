# Routing and bookmarkability context

The supplied bookmarkability proposal ultimately moves major peer sections and durable
agent details to canonical paths. This bundle does not implement that route model.

It prepares the component layer by introducing URL-independent typed state:

- `AgentWorkspaceSection` for the current top-level workspace section;
- `AgentsWorkspaceState` for selected agent/team, usage selection, Simple Chat state, and
  active agent-details target;
- `AgentDetailsSection` for the ten editor sections;
- `AgentDetailsRequest` for create/edit target and initial section;
- typed catalog intents rather than child-owned navigation or dialogs.

`AgentWorkspaceRouteState` remains the compatibility codec for current `/agents` query
state. It maps to/from the typed workspace state without adding, removing, or reordering
current query keys.

The future routing bundle should therefore bind URLs to an existing state model rather
than redesigning component APIs again.
