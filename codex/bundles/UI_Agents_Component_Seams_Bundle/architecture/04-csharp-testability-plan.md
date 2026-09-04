# C# testability plan

## State and pure mapping tests

Directly instantiate typed state/mapping types. Required durable cases:

1. current recognized route state maps to `AgentsWorkspaceState` and back without URL
   change;
2. invalid/obsolete section input canonicalizes to Overview;
3. `AgentDetailsSection` has a stable explicit order independent of enum numeric values.

Do not test private page fields.

## Overview query tests

Instantiate `AgentsOverviewQuery` through the smallest available service fixture. Cover:

1. successful aggregate with overview, usage, HR agent/avatar map, and bound count;
2. missing managed HR agent becomes the existing warning/empty presentation rather than
   a fabricated agent;
3. usage partial-source errors remain visible while valid totals are retained.

The component test supplies a fake `IAgentsOverviewQuery`; it does not construct EF.

## Catalog controller and component tests

Controller tests cover load/repair policy, privacy projection, member mutation/reload,
and delete/reload.

Component tests render `AgentCatalogPanel` with explicit state and capture intents. They
must not register Workspace, provider, repair, dialog, notification, or chat services.

Page composition tests cover requested-agent open-once behavior and result-driven catalog
reload/selection changes.

## Editor controller and component tests

Controller tests cover core load, partial provider/secret errors, lazy projects, save
normalization, delete, capability persistence, and verification using focused cases.
Avoid duplicating every field-level UI test at controller level.

All six existing details test classes render the real `AgentDetailsDialog` using:

- `AgentEditorSession`;
- `AgentDetailsSection`;
- one shared fake `IAgentEditorController`/test harness;
- real public callbacks/results.

No test subclass, private reflection, or uninitialized production service remains.

## Durable dependency guard

Add a small reflection-based architecture test over public/component metadata, not source
text, asserting forbidden injection absence:

- `AgentsHomePage` does not inject `IDbContextFactory<>`;
- `AgentCatalogPanel` has no `[Inject]` properties;
- `AgentDetailsDialog` does not inject Workspace, provider administration, Projects,
  Secrets, external-target registry, EF, or `IServiceProvider`.

Do not assert the exact allowed dependency count or private field list.

## Expected new focused unit discovery

Plan 18 durable unit cases across workspace state, overview query, catalog controller,
editor controller, and forbidden-dependency guards. SB01/SB02 must freeze exact method
names and discovery before relying on this number. Existing route-state discovery remains
10.

The 46 primary component cases remain the behavior baseline; rewrites should preserve the
count unless a documented behavior consolidation/addition is approved before changing it.
