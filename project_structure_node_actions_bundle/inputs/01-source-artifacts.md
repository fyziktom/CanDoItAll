# Source Artifacts

## Primary Input

- User request from 2026-04-30, preserved in `inputs/00-original-request.md`.

## Repo Discovery Evidence

- CodeAnalytics snapshot: `snap-20260430233239-37492784`.
- Solution: `C:/repositories/CanDoItAll/CanDoItAll.slnx`.
- Target framework observed in relevant projects: `net10.0`.

## Component MCP Evidence

- `ContextMenuHost` from CanvasLib is the shared canvas context-menu host. Source: `C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Components/Graph/Overlays/ContextMenuHost.razor`.
- `ContextMenuHostFactory.CreateForWorkbench` builds context-menu snapshots from node actions. Source: `C:/repositories/CanDoItAll/src/CanDoItAll.Components.CanvasLib/Canvas/Graph/Chrome/ContextMenuHost.cs`.
- `ContextMenu` from BaseLib is the general navigation menu pattern. Source: `C:/repositories/CanDoItAll/src/CanDoItAll.Components.BaseLib/Components/Navigation/ContextMenu.razor`.

## Exact Source Inventory

| Surface | File |
| --- | --- |
| Project structure page and double-click quick-action flow | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` |
| Quick-action dialog construction and execution | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeQuickActions.cs` |
| Inspector actions, right-panel action list, and action dispatch | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeEditing.cs` |
| Runtime launch host integration | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.RuntimeLaunch.cs` |
| Runtime launch plan and elevated PowerShell execution | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs` |
| Local File Explorer launch guard | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs` |
| Canvas right-click action catalog | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureActionCatalogAdapter.cs` |
| Context-menu ordering and first-ring priority | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureMenuComposition.cs` |
| Action shortcut assignment | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureActionShortcuts.cs` |
| Storage provider and locator model | `C:/repositories/CanDoItAll/src/CanDoItAll.Infrastructure/Storage/Models/StorageModels.cs` |
| Managed-file/IPFS endpoint behavior | `C:/repositories/CanDoItAll/src/CanDoItAll.Web/Infrastructure/ManagedFilesEndpointRoutes.cs` |
| Agent-facing project-structure contracts | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentContracts.cs` |
| Agent service summary mapping | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs` |
| Project Structure MCP tool descriptions | `C:/repositories/CanDoItAll/src/CanDoItAll.Mcp.ProjectStructure/ProjectStructureTools.cs` |
| Internal agent project-structure tools | `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs` |
| Action catalog tests | `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProjectStructureActionCatalogAdapterTests.cs` |
| Project structure page component tests | `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs` |
| Runtime launcher unit tests | `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/ProjectStructureRuntimeLauncherTests.cs` |
| Runtime path resolver tests | `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/ProjectStructureRuntimeLauncherPathResolverTests.cs` |
| Local file opener tests | `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Unit/ProjectStructureLocalFileOpenerManagedFilesTests.cs` |
| MCP project-structure tests | `C:/repositories/CanDoItAll/tests/CanDoItAll.Mcp.ProjectStructure.Tests/ProjectStructureToolsTests.cs` |
| Agent API integration tests | `C:/repositories/CanDoItAll/tests/CanDoItAll.Tests.Integration/ProjectStructureAgentApiIntegrationTests.cs` |
