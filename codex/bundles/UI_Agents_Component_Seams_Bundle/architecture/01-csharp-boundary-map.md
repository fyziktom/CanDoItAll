# C# target boundary map

## Page-owned state

`AgentsHomePage` owns:

- `AgentsWorkspaceState`;
- top-level `AgentWorkspaceSection`;
- selected agent and team identity;
- current Simple Chat state and usage selection;
- active `AgentDetailsRequest` and route-request duplicate suppression;
- catalog loading/error state returned by `IAgentCatalogController`;
- decisions to navigate, open route-significant details, open team/editor dialogs, launch
  managed chats, and present notifications.

## Component-local state

`AgentCatalogPanel` owns only:

- draft search text;
- expanded tree-node IDs;
- hover/focus and purely visual state.

`AgentDetailsDialog` owns:

- the mutable editor draft for the current open editor;
- local busy/loading/error presentation derived from controller results;
- confirmation and wizard presentation state;
- draft filters within the Capabilities section;
- a render adapter between `AgentDetailsSection` and the current Tabs index.

It does not own the durable section identity outside the dialog; every section change is
reported through the typed callback.

## Controller/query ownership

### IAgentsOverviewQuery

One read workflow that returns a typed aggregate containing the existing overview and
usage snapshots, managed HR-agent resolution, avatar lookup, bound-resource count, and
partial-warning information. It owns database and multi-source orchestration. It has no
navigation, dialogs, or notifications.

### IAgentCatalogController

One catalog workflow that:

- optionally ensures organization catalog repair;
- loads/reloads agents, teams, and provider privacy metadata;
- updates team members;
- deletes teams;
- returns a typed snapshot after mutations.

It does not open dialogs, navigate, launch chat, store RenderFragments, or own page
selection.

### IAgentEditorController

One editor workflow that:

- loads an existing/new editor session and supporting catalogs;
- preserves separate provider/secret partial-failure information;
- lazy-loads project access items;
- refreshes providers/capabilities;
- normalizes and saves the draft;
- deletes an agent;
- persists capability assignment for an existing agent;
- verifies a capability and returns refreshed state.

It does not present notifications, open dialogs, or own Tabs/rendering state.

## Presentation contract

`AgentCatalogPanel` receives one `AgentCatalogViewState` and emits one
`EventCallback<AgentCatalogIntent>`. Do not add a wrapper Razor component around it.

`AgentDetailsDialog` receives `AgentDetailsSection`, optional `AgentEditorSession`, and a
controller through DI. It emits `SelectedSectionChanged` and the existing semantic
save/delete completion result.
