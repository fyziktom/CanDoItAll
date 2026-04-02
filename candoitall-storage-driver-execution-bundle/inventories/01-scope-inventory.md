
# Scope Inventory

## In-scope code areas

- Infrastructure storage baseline:
  - `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/Storage/WorkspaceStorage.cs`
  - `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/DependencyInjection/InfrastructureServiceCollectionExtensions.cs`
  - `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs`
  - `C:\repositories\CanDoItAll/src/CanDoItAll.Infrastructure/ControlPlane/DatabaseSnapshots.cs`
- Persisted upload/export adopters:
  - Workbench files, imports, generated artifacts, previews
  - Prompt Factory attachments and exports
  - seed/profile artifact flows
- UI management surfaces:
  - `SettingsPage.razor`
  - settings list/detail component patterns
  - shared component library
  - workbench create/edit/preview surfaces
  - prompt factory attachment surfaces
- Tests:
  - unit, integration, Playwright automation
  - manual Playwright MCP proof
  - fake IPFS harness; honest FTP proof path

## Adjacent but explicitly inventoried surfaces

- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Resources/ResourceModels.cs`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Modules.Resources/Pages/ResourcesPage.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Web/Components/Layout/MainLayout.razor`
- `C:\repositories\CanDoItAll/src/CanDoItAll.Mcp.SshOps/Transport/SshNetTransport.cs`

## Out-of-immediate-scope internal file I/O

The bundle does **not** instruct Codex to convert every internal repo-local file read/write in MCP/runtime/support packages into storage-driver traffic. Those surfaces are inventoried only so they are not mistaken for unowned omissions.

