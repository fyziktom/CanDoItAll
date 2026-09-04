# State and intent contract

## AgentWorkspaceSection

Stable semantic values corresponding to current visible sections:

```text
Overview
Agents
SimpleChats
Providers
RequestHistory
Voice
FloatingChat
Chat
Capabilities
Governance
Diagnostics
```

Do not rely on enum numeric values. `AgentWorkspaceTabs` remains the current lower-case
external key mapping.

## AgentsWorkspaceState

Required semantic fields:

```text
Section
SelectedAgentId
SelectedTeamId
SimpleChat
UsageSelection
ActiveAgentDetails
```

The implementation may include an immutable navigation generation or a separate
page-transient duplicate-suppression field, but it must not put private child echo state
back into `AgentCatalogPanel`.

Loading/error/dashboard snapshots are separate view data, not URL/navigation state.

## AgentDetailsSection

Stable explicit order matching current UI:

```text
Identity
Runtime
Memory
Images
ProjectStructureAccess
WorkspaceTools
Secrets
ProcessAccess
Capabilities
Voice
```

A mapper owns conversion to/from the Tabs index. Tests use the enum, never raw indexes.

## AgentDetailsRequest

Represents:

```text
AgentId: null for create, durable ID for edit
Section: initial/current stable editor section
```

The page owns the request and open-once/dismissal behavior. The dialog reports section
changes. No URL key is added in this bundle.

## AgentCatalogViewState

Contains existing agent/team models, provider-privacy projection, selected IDs,
loading/error state, and any stable accessibility summary needed by rendering. It must
not duplicate the entire domain model into new DTO classes.

## AgentCatalogIntent

Required intent cases:

```text
SelectAgent(agentId)
SelectTeam(teamId or null)
OpenAgentDetails(agentId or null)
OpenTeamDetails(teamId or null)
OpenTeamMembers(teamId)
DeleteTeam(teamId)
OpenManagedChat(agentId)
RetryLoad
```

Add a case only when it represents a user intent already present in the component.
Intents carry IDs or stable values, not component references or RenderFragments.

## State classification

| State | Owner | Future URL eligibility |
|---|---|---|
| top-level section | page/workspace | yes |
| selected agent/team | page/workspace | yes, after routing bundle decision |
| details target and section | page/workspace | yes |
| usage selection / Simple Chat state | page/workspace | already represented in current query state |
| catalog data/loading/error | page/controller view state | no direct URL |
| catalog search/tree expansion | component-local | optional future filter only after explicit decision |
| editor draft and validation | dialog-local | no |
| capability draft filters | dialog-local | no |
| busy/confirmation/hover/focus | component-local | no |
