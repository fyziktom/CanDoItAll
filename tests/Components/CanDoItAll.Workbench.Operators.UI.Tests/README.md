# Workbench Operators UI tests

Independent bUnit tests for the actual Operators UI leaf, with no Workbench/CRM/Workspace
implementation module reference. They exercise raw inputs, callback admission, retained
opening state, explicit read failure, missing selections, sensitive projection and independent
instances. Native persistence and policy tests remain in CanDoItAll.Tests.Components.

Run `dotnet test tests/Components/CanDoItAll.Workbench.Operators.UI.Tests` from the repository
root. For WB5 evidence, build production first, refresh test discovery, and execute the
same configuration without rebuilding. See [Testing](../../../docs/testing.md).
