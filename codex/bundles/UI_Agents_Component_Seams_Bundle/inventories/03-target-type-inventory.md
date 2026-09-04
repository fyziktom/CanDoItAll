# Target type inventory

Names below are the prepared contract. Equivalent naming requires a pattern-decision
addendum; responsibility may not change silently.

| Type | Kind | Owner |
|---|---|---|
| `AgentWorkspaceSection` | enum | page semantic state |
| `AgentsWorkspaceState` | immutable record | route-owning page/workspace |
| `AgentDetailsSection` | enum | stable detail section identity |
| `AgentDetailsRequest` | immutable record | page-owned detail target |
| `AgentsOverviewViewState` | aggregate record reusing existing models | overview query result |
| `IAgentsOverviewQuery` / `AgentsOverviewQuery` | read workflow seam | application/query layer |
| `AgentCatalogSnapshot` | immutable data snapshot | catalog controller result |
| `AgentCatalogViewState` | controlled component state | page presentation state |
| `AgentCatalogIntent` hierarchy | typed user intents | catalog component output |
| `IAgentCatalogController` / `AgentCatalogController` | catalog workflow seam | application/controller layer |
| `AgentEditorSession` | existing editor + supporting reference state | editor boundary |
| `AgentEditorLoadRequest` | load inputs | editor controller contract |
| `AgentEditorSaveResult` | save outcome and refreshed state as needed | editor controller contract |
| `IAgentEditorController` / `AgentEditorController` | editor workflow seam | application/controller layer |

Do not create duplicate copies of `AgentDefinition`, `AgentEditorModel`, `ProviderProfile`,
`CapabilityCatalogItem`, `ProjectAccessListItem`, or `SecretListItem` merely to rename them
for UI.
