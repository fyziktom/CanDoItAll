# API Access UI tests

Backend-free xUnit/bUnit tests for the actual API renderers, shared presentation controllers
and bounded scenario owners. The project references the independent sandbox and cannot
load the Workspace implementation, database, JWT or password runtime through that graph.

From the repository root, discover before executing:

```powershell
dotnet test tests/Components/CanDoItAll.Workspace.ApiAccess.UI.Tests -c Debug --list-tests --filter 'FullyQualifiedName~CanDoItAll.Tests.Components.WorkspaceApiUi' /m:1
dotnet test tests/Components/CanDoItAll.Workspace.ApiAccess.UI.Tests -c Debug --no-build --no-restore --filter 'FullyQualifiedName~CanDoItAll.Tests.Components.WorkspaceApiUi' /m:1
```

Real owner durability, HTTP/session enforcement and source/published browser journeys live
in their respective Unit, Integration and Playwright projects. They supply different proof;
these scenario tests do not certify security or filesystem commit semantics. See
[the validation record](../../../docs/architecture/workspace-api-access-ui-boundary.md).
