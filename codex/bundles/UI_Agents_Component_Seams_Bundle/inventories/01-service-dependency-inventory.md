# Service dependency inventory

## Before

| Component | Injected services | Target disposition |
|---|---:|---|
| `AgentsHomePage` | 8 | keep host services; replace Workspace/usage/EF aggregation with overview/catalog seams |
| `AgentCatalogPanel` | 6 | remove all feature injections; state + intent parameters only |
| `AgentDetailsDialog` | 7 | retain dialog/notification; replace external workflow services with editor controller |

## Planned production seams

| Seam | Cohesive responsibility | Implementation dependencies allowed |
|---|---|---|
| `IAgentsOverviewQuery` | assemble one Agents dashboard read result | Workspace, usage query, EF/context and CRM-HR binding model |
| `IAgentCatalogController` | catalog load/repair/privacy and team mutation/reload | Workspace, provider runtime admin, catalog repair |
| `IAgentEditorController` | editor load/reference/save/delete/capability workflow | Workspace, provider admin, Projects, Secrets, external-target registry |

## Allowed direct host dependencies

- `NavigationManager` only at route-owning page;
- `DialogService` and `NotificationService` at page/top-level editor;
- `IAgentChatLauncher` at page for managed chat;
- child component-specific technical services outside this target remain untouched.

## Forbidden replacement

Do not introduce:

```text
IAgentsServices
AgentServiceBag
IComponentServiceProvider
AgentCatalogPanelContainer
AgentDetailsDialogPresenterBase
```

A facade that forwards each old method unchanged is not an accepted seam.
