# C# dependency direction

## Current direction

```mermaid
graph TD
    Home[AgentsHomePage Razor] --> Workspace[Workspace service]
    Home --> Usage[Usage query]
    Home --> Db[AppDbContext / EF]
    Home --> Host[Navigation / dialogs / notifications / chat]

    Catalog[AgentCatalogPanel Razor] --> Workspace
    Catalog --> ProviderAdmin[Provider runtime admin]
    Catalog --> Repair[Catalog repair]
    Catalog --> Host

    Details[AgentDetailsDialog Razor] --> Workspace
    Details --> ProviderAdmin
    Details --> Projects[ProjectsService]
    Details --> Secrets[SecretService]
    Details --> Infra[External target registry]
    Details --> Host
```

The Razor layer points directly into several application, cross-module, and
infrastructure concerns.

## Target direction in the existing project

```mermaid
graph TD
    Route[Current /agents route codec] --> Home[AgentsHomePage]
    Home --> State[Typed workspace state and pure mappings]
    Home --> OverviewPort[IAgentsOverviewQuery]
    Home --> CatalogPort[IAgentCatalogController]
    Home --> CatalogView[AgentCatalogPanel controlled view]
    CatalogView --> Intent[AgentCatalogIntent]
    Intent --> Home
    Home --> Host[Navigation / dialogs / notifications / chat]

    OverviewImpl[AgentsOverviewQuery] --> Workspace
    OverviewImpl --> Usage
    OverviewImpl --> Db[EF / CRM-HR binding model]
    CatalogImpl[AgentCatalogController] --> Workspace
    CatalogImpl --> ProviderAdmin
    CatalogImpl --> Repair

    Details[AgentDetailsDialog] --> EditorPort[IAgentEditorController]
    Details --> EditorHost[Dialog / notification presentation]
    EditorImpl[AgentEditorController] --> Workspace
    EditorImpl --> ProviderAdmin
    EditorImpl --> Projects
    EditorImpl --> Secrets
    EditorImpl --> Infra
```

Dependencies move outward from Razor through three cohesive workflow contracts. The
implementation types remain in the current module for now.

## Future physical direction

After a later extraction bundle:

```text
CanDoItAll.Modules.AgentFramework.UI
    -> AgentFramework UI contracts/read models
    -> AppComponents
    -> shared Components/FileTools

CanDoItAll.Modules.AgentFramework (application/composition side)
    -> implements overview/catalog/editor contracts
    -> Workspace, ProviderManagement, Projects, Security, Infrastructure
```

This bundle must not add that project or change project references. It only makes the
future split possible.

## Cycle gate

No project-reference change is expected. Any new project reference or cycle is a bundle
repair trigger, not an implementation detail.
