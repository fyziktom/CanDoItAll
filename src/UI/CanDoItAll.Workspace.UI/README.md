# CanDoItAll.Workspace.UI

## Purpose

The actual Settings shell and defaults, Secrets, Files and provider history renderers.
Uses BaseLib fields, layout, SecretField, copy controls and confirmation dialog. The shell
accepts active deferred slots; Data Sources, Storage and API Access stay in production.

## Project Type

.NET 10 Razor class library. State and intent contracts expose no backend services.

## Dependencies

See [the project](CanDoItAll.Workspace.UI.csproj). Only BaseLib, Workspace.Contracts and
ProviderHistory.Abstractions are direct project dependencies. Raw numeric history input
remains invalid until corrected; no clamping hides a parse or range error.

## Validation

```powershell
dotnet build src/UI/CanDoItAll.Workspace.UI --configuration WorkspaceSettingsUiProof /m:1
```

Use [the sandbox](../../Sandboxes/CanDoItAll.Workspace.UiSandbox/README.md) for interactions
and [the boundary record](../../../docs/architecture/workspace-settings-core-ui-boundary.md)
for owner and production proof.
