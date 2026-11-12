# Workbench Execution UI tests

Independent bUnit tests for the actual Execution renderers and dynamic child dialogs.
They verify raw Workflow inputs, captured callbacks, concurrent view independence,
typed candidate choices, cancelled switches, retired parents and accepted read-only details.
Native persistence, authority, preparation and recovery tests remain in
CanDoItAll.Tests.Components.

Run `dotnet test tests/Components/CanDoItAll.Workbench.Execution.UI.Tests` from the repository
root. Build production first and verify discovery before executing the same configuration.
See [Testing](../../../docs/testing.md).
