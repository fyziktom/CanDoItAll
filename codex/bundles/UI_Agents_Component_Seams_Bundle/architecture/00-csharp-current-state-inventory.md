# C# current-state inventory

## Primary symbols

| Symbol | Current ownership | Direct dependencies | Hidden/overlapping state | Testability issue |
|---|---|---|---|---|
| `AgentsHomePage` | route, dashboard, data aggregation, dialogs, navigation, chat | 8 injected services including EF | string tab plus scalar IDs/flags and child callbacks | full component harness needed for data orchestration |
| `AgentCatalogPanel` | data load, repair, selection, dialogs, mutations, chat, view | 6 injected services | selected IDs and requested-dialog echo suppression also owned by page | reflection into private fields/methods |
| `AgentDetailsDialog` | editor load, all reference catalogs, draft, commands, dialogs | 7 injected services including concrete Projects/Security and infrastructure | numeric section index; private seeded data | subclass + private reflection + uninitialized concrete services |
| `AgentWorkspaceRouteState` | current query parse/build compatibility | static mapping | string tab identity | route model not yet separated from UI semantic state |
| `AgentFrameworkUiServiceCollectionExtensions` | limited UI registrations | DI | no registrations for the planned seams | composition must be extended explicitly |

## Existing partial-class policy

The target page/components use normal Razor code-behind partials. That framework-required
shape may remain. No additional `AgentsHomePage.*.cs`, `AgentCatalogPanel.*.cs`, or
`AgentDetailsDialog.*.cs` partial files may be added as an extraction mechanism.

## Current tests

- 46 focused component cases in the primary slice;
- 10 route-state unit cases;
- one adjacent Workflows test reflects a private AgentsHomePage method;
- six AgentDetails test classes duplicate a private-field test subclass pattern.

## CodeAnalytics evidence

SB01 must record a current CodeAnalytics snapshot, dashboard health, source symbols,
dependency/cycle result, and relevant hotspots when the MCP is available. If unavailable,
record the gap and use direct source/project inspection; do not fabricate metrics.
