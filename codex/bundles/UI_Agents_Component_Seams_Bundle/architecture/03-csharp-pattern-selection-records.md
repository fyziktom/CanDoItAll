# C# pattern selection records

## PSR-01 — Typed workspace state

**Decision:** introduce `AgentWorkspaceSection` and `AgentsWorkspaceState`; retain
`AgentWorkspaceTabs` and `AgentWorkspaceRouteState` as compatibility mapping surfaces.

**Reason:** the page needs one semantic state owner before later URL migration.

**Rejected:** continue using string tab keys and scalar fields; place query knowledge in
child components; implement new routes now.

## PSR-02 — Aggregate overview query

**Decision:** introduce `IAgentsOverviewQuery` with one implementation and one typed
aggregate result reusing existing domain/read models.

**Reason:** the page currently assembles one dashboard snapshot from Workspace, usage,
managed-agent, avatar, and EF sources. This is one cohesive read workflow.

**Rejected:** one interface per metric; moving the same private page methods into another
partial; returning an untyped dictionary.

## PSR-03 — Controlled AgentCatalogPanel

**Decision:** keep the existing `AgentCatalogPanel` component but make it receive
`AgentCatalogViewState` and emit `AgentCatalogIntent`. It has no feature-service
injection. `AgentsHomePage` owns host intent handling; `IAgentCatalogController` owns
catalog data/mutations.

**Reason:** this removes duplicate state and hidden host actions without adding a wrapper
component.

**Rejected:** `AgentCatalogPanelContainer -> AgentCatalogPanelView`; keeping dialog/chat
launch in the child; a controller that stores component instances or navigation state.

## PSR-04 — Typed editor section and explicit session

**Decision:** introduce stable `AgentDetailsSection`, an explicit ordered mapping to the
current Tabs control, `AgentDetailsRequest`, and `AgentEditorSession`.

**Reason:** numeric indexes are not durable identities and private field seeding blocks
sandbox/tests.

**Rejected:** exposing `selectedTabIndex`; creating ten wrapper editors immediately;
changing the dialog into a routed page in this bundle.

## PSR-05 — Cohesive editor controller

**Decision:** introduce one `IAgentEditorController` and implementation for the editor's
external load/save/delete/reference/capability workflows. The controller may use current
Workspace, ProviderManagement, Projects, Security, and infrastructure services
internally.

**Reason:** the controller is the real component/sandbox substitution boundary. Creating
separate ports for every existing service would add maintenance without changing the
current project graph.

**Rejected:** one UI interface per underlying service; a pass-through service bag; leaving
save normalization and persistence in the Razor component.

**Constraint:** a fourth production interface is not pre-approved. If the concrete
controller cannot be tested without full web-host construction, stop and add a written
PSR addendum describing the smallest real missing boundary before proceeding.

## PSR-06 — Test harness through public seams

**Decision:** render the real components using typed state/session and fake controllers;
consolidate repeated details test setup into one shared test harness.

**Reason:** protects behavior while permitting internal simplification.

**Rejected:** private reflection, subclass field mutation, uninitialized concrete
services, exact private method/file assertions, or a permanent test of dependency counts.
