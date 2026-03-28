# Scope Inventory

## Existing Code Surfaces Likely To Change

- `C:\repositories\CanDoItAll\src\CanDoItAll.Web\Program.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectWorkbenchModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\WorkspaceModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Pages\SettingsPage.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\WorkspaceModuleServiceCollectionExtensions.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\Concurrency\ResourceMutationGate.cs`
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\.vscode\mcp.json`

## New Project Surfaces Likely Needed

- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.ProjectStructure.Tests\`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Mcp.ProjectStructure.IntegrationTests\`
- `C:\repositories\CanDoItAll\tools\CanDoItAll.Mcp.ProjectStructure\`
- `C:\repositories\CanDoItAll\CanDoItAll.Mcp.ProjectStructure.settings.json`

## Validation Surfaces

- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Integration\`
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Playwright\`
- `C:\repositories\CanDoItAll\project-structure-mcp-bundle-1\reviews\01-execution-report.md`

## Rollout Inventory

- Local MCP config update
- Release publish path update
- Settings template
- README or setup snippet output
- Skill or script sync only if the new rollout flow genuinely needs a repo-local skill addition
