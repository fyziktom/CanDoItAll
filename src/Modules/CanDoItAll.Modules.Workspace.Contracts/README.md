# CanDoItAll.Modules.Workspace.Contracts

## Purpose

Light contracts for the six workspace defaults, provider choices and typed Settings owner
ports. Existing `CanDoItAll.Modules.Workspace` namespaces, values and wire fields remain
stable. Secret metadata and transient editor values come from Security.Abstractions.

## Project Type

.NET 10 class library. No persistence, vault, provider implementation or Blazor dependency.

## Dependencies

See [the project](CanDoItAll.Modules.Workspace.Contracts.csproj). Owners return explicit
refusal or known commit results. Unclassified exceptions remain uncertain outcomes.

## Validation

```powershell
dotnet build src/Modules/CanDoItAll.Modules.Workspace.Contracts --configuration WorkspaceSettingsUiProof /m:1
```

See [the boundary and validation record](../../../docs/architecture/workspace-settings-core-ui-boundary.md).
