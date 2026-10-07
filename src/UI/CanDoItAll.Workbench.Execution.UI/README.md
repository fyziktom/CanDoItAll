# Workbench Execution UI

Independent Blazor presentation for Workflow add/start and Process link, confirmation,
staffing, candidate picker, details and switch confirmation. Immutable display snapshots
and captured callbacks describe one opening. Actual child dialogs carry typed opening,
role and candidate identities. The native host retains authority, preparation and execution.

This leaf references neutral Components, RecordBrowsing and AgentFramework display models.
It has no persistence, provider execution or Workbench/Process implementation reference.
Build with `dotnet build` on this project. The independent Execution sandbox and tests
consume these production renderers, including dynamically opened children.

See [the architecture record](../../../docs/architecture/workbench-execution-ui.md).
