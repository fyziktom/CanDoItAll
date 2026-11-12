# Workspace Data Sources UI

The actual profile editor and transfer dialog depend only on neutral Data Sources
contracts and BaseLib. `DataSourcesSession` owns an acquired draft and independent reads;
`DataSourceTransferSession` captures one dialog lifetime. `DataSourceOperationLedger`
retains safe original-target outcomes across panel/dialog closure in the same circuit.
An unacknowledged or partial operation holds further writes; reads never replay it.

The production host supplies `WorkspaceDataSourcesOwner`. The
[standalone scenario host](../../Sandboxes/CanDoItAll.Workspace.DataSources.UiSandbox/README.md)
supplies deterministic scenarios without database drivers. Existing profile encryption,
schema bootstrap, transfer handlers and canonical runtime authority remain with their owners.
