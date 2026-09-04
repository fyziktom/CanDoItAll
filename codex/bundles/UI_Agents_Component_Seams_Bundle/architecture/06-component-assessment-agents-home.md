# Component boundary assessment — AgentsHomePage

## Identity

- current owner: `CanDoItAll.Modules.AgentFramework`
- current location remains unchanged
- future likely destination: route host/composition side plus a smaller Agents UI project

## Rendering responsibility

Shell header, top-level section composition, overview dashboard, and agent-chat context
surface.

## Current semantic state

String tab key; effective agent/team IDs; Simple Chat state; usage selection; selected
agent/team context; HR agent; overview/usage snapshots; load/error/busy flags.

## Direct dependencies to remove

- `IDbContextFactory<AppDbContext>`;
- direct bound-resource EF query;
- direct multi-source dashboard aggregation from Workspace and usage services.

## Dependencies justified after refactor

- `NavigationManager` as route owner;
- `DialogService` and `NotificationService` as top-level host presentation;
- `IAgentChatLauncher` for global managed chat;
- `AgentFrameworkCatalogWarmupService` for the explicit load-defaults action;
- `IAgentsOverviewQuery` and `IAgentCatalogController` as cohesive feature seams.

## Target state ownership

The page owns `AgentsWorkspaceState` and active agent-details target. Child components do
not construct URLs or suppress route echo privately.

## Selected extraction choices

- pure enum/state/route mapping;
- aggregate overview query;
- page-owned typed intent handling.

## Acceptance

- current route/query behavior unchanged;
- no direct EF or `AiResourceBinding` query in Razor;
- deep-link agent detail opens exactly once;
- dashboard partial-error behavior and HR identity semantics preserved;
- no new partial file.

## Readiness after bundle

- route-ready: yes for state binding, route implementation deferred;
- sandbox-ready: page partially, using overview/catalog fakes; full shell still host-bound;
- project-extraction-ready: partial, blocked by remaining tab/module composition.
