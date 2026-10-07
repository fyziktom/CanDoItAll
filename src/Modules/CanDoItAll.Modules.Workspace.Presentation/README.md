# CanDoItAll.Modules.Workspace.Presentation

## Purpose

Independent controllers for defaults, Secrets, Files and versioned provider history.
They own editor admission, immutable operation capture, read generations, reconciliation
and bounded redacted receipts. Production and the scenario host use the same controllers.

## Project Type

.NET 10 class library with the ASP.NET Core framework reference for EditContext.

## Dependencies

See [the project](CanDoItAll.Modules.Workspace.Presentation.csproj). Typed ports lead toward
light contracts; production owner adapters remain in the Workspace module. History reuses
the existing ProviderHistory.Abstractions service and policy types.

## Validation

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Workspace.Presentation --configuration WorkspaceSettingsUiProof /m:1
dotnet test tests/Components/CanDoItAll.Workspace.UI.Tests --configuration WorkspaceSettingsUiProof --filter FullyQualifiedName~WorkspaceUi /m:1
```

See [the boundary record](../../../docs/architecture/workspace-settings-core-ui-boundary.md).
