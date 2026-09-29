# CanDoItAll.Workspace.UiSandbox

## Purpose

Standalone Settings Core scenario host using the actual shell, four renderers and production
presentation controllers. Its mutable synthetic stores expose held operations, explicit
faults, reset, missing references and profile retirement. Late admitted writes belong to
the original store. Receipts never retain payloads or full commands.

## Project Type

.NET 10 interactive server host. It has no database, vault, API, provider account, file
launcher or worker configuration. Deferred surfaces are explicitly outside this proof.

## Dependencies and assets

See [the project](CanDoItAll.Workspace.UiSandbox.csproj). The evaluated closure has eight
projects. Asset mode is Parity: BaseLib and font assets plus the application's generated
`wwwroot/css/output.css` linked as content. There is no Web project reference. Follow the
Web project's existing theme build after changing authoritative theme inputs; this sandbox
does not generate a competing theme. The linked application CSS and `Tailwind/input.css` are
absent from the sandbox's .NET watch inventory. Run `npm ci --prefix Tailwind` if dependencies
are absent, then `npm run tailwind:build`, rebuild/restart the sandbox and refresh the browser.
An independently owned `npm run tailwind:watch` can regenerate the same canonical output.
No feature scoped CSS or feature JavaScript was added.

## Run and publish

From the repository root, use a free task-owned port:

```powershell
dotnet watch --project src/Sandboxes/CanDoItAll.Workspace.UiSandbox --configuration WorkspaceSettingsUiProof --no-launch-profile -- --urls http://127.0.0.1:5149
dotnet publish src/Sandboxes/CanDoItAll.Workspace.UiSandbox --configuration WorkspaceSettingsUiProof --output artifacts/workspace-settings-sandbox-publish /m:1
```

Run the published DLL with its output directory as the working directory and both
`ASPNETCORE_ENVIRONMENT` and `DOTNET_ENVIRONMENT` set to `Production`. The scenario host uses
synthetic strings only. Do not paste real secrets. Reset creates a new store and editor
lifetime; release completes a held operation in its original store.

## Validation

```powershell
dotnet test tests/Components/CanDoItAll.Workspace.UI.Tests --configuration WorkspaceSettingsUiProof --filter FullyQualifiedName~WorkspaceUi /m:1
```

See [the proof record](../../../docs/architecture/workspace-settings-core-ui-boundary.md)
for source/published browser, graph and hot reload results. Scenario effects do not establish
real persistence, transaction, encryption or authority behavior.
