# Processes UI

Shared Razor renderers for the global and project Processes and Live Processes workspaces.
The library owns presentation and opening-scoped draft state. Native hosts own reads,
writes, authority, accepted outcomes and reconciliation through typed session contracts.

The eight renderer families cover the workspace, live workspace, canvas, role and step
editors, template library, run files and cancellation. The same components run in the
[independent sandbox](../../Sandboxes/CanDoItAll.Processes.UiSandbox/README.md).

The dependency boundary reuses neutral process projections and shared UI components.
It does not reference native modules, persistence or execution services. The light test
assembly checks the actual resolved closure and public component parameters.

See the [boundary and ownership record](../../../docs/architecture/processes-ui-boundary.md)
and [validation instructions](../../../docs/testing.md#processes-ui-slice). Native authoring
durability is a separate, explicitly unresolved acceptance item in that record.
