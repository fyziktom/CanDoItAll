# Requirement Traceability

## Input Coverage Matrix

| Raw note | Requirement ids | Impacted surface | Planned proof method | Owning subbundle | Dependency signal | Exception status |
| --- | --- | --- | --- | --- | --- | --- |
| `N001` | `REQ-RUN-001`, `REQ-RUN-002` | Double-click quick-action modal | Component tests plus Playwright modal proof | `01-runtime-node-run-actions` | Foundation for `N002`, `N005` | None |
| `N002` | `REQ-RUN-003`, `REQ-RUN-004` | Canvas right-click context menu and runtime launcher dispatch | Action catalog tests plus Playwright context-menu proof | `01-runtime-node-run-actions` | Foundation for `N005` | None |
| `N003` | `REQ-FILE-001`, `REQ-FILE-002` | File nodes, quick-action modal, context menu, local opener | Unit tests plus Playwright action visibility proof | `02-file-and-ipfs-open-actions` | Depends on action pattern from subbundle 01 | None |
| `N004` | `REQ-FILE-003`, `REQ-FILE-004` | IPFS nodes and browser new-tab action | Component/unit tests plus Playwright action visibility proof | `02-file-and-ipfs-open-actions` | Depends on file detection model | None |
| `N005` | `REQ-TOOLS-001`, `REQ-TOOLS-002`, `REQ-TOOLS-003` | Project Structure MCP and internal agent project-structure tools | Targeted MCP/agent tests plus contract inspection | `03-mcp-and-internal-agent-action-contracts` | Depends on subbundles 01 and 02 | No remote host launch API will be added |

## Requirement Destinations

| Requirement | Bundle destination | Exact implementation references |
| --- | --- | --- |
| `REQ-RUN-001` | `subbundles/01-runtime-node-run-actions` | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeQuickActions.cs`, `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.razor` |
| `REQ-RUN-002` | `subbundles/01-runtime-node-run-actions` | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.RuntimeLaunch.cs`, `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs` |
| `REQ-RUN-003` | `subbundles/01-runtime-node-run-actions` | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureActionCatalogAdapter.cs`, `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureMenuComposition.cs` |
| `REQ-RUN-004` | `subbundles/01-runtime-node-run-actions` | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeEditing.cs`, `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureRuntimeLauncher.cs` |
| `REQ-FILE-001` | `subbundles/02-file-and-ipfs-open-actions` | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs`, `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeQuickActions.cs` |
| `REQ-FILE-002` | `subbundles/02-file-and-ipfs-open-actions` | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureLocalFileOpener.cs` |
| `REQ-FILE-003` | `subbundles/02-file-and-ipfs-open-actions` | `C:/repositories/CanDoItAll/src/CanDoItAll.Infrastructure/Storage/Models/StorageModels.cs`, `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeQuickActions.cs` |
| `REQ-FILE-004` | `subbundles/02-file-and-ipfs-open-actions` | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/Pages/ProjectStructurePage.NodeEditing.cs`, `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/CanvasAdapters/ProjectStructureActionCatalogAdapter.cs` |
| `REQ-TOOLS-001` | `subbundles/03-mcp-and-internal-agent-action-contracts` | `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentContracts.cs`, `C:/repositories/CanDoItAll/src/CanDoItAll.Modules.Workbench/ProjectStructure/ProjectStructureAgentService.cs`, `C:/repositories/CanDoItAll/src/CanDoItAll.Mcp.ProjectStructure/ProjectStructureTools.cs` |
| `REQ-TOOLS-002` | `subbundles/03-mcp-and-internal-agent-action-contracts` | `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs` |
| `REQ-TOOLS-003` | `subbundles/03-mcp-and-internal-agent-action-contracts` | `C:/repositories/CanDoItAll/src/CanDoItAll.Mcp.ProjectStructure/ProjectStructureTools.cs`, `C:/repositories/CanDoItAll/src/CanDoItAll.AgentFramework.Maf/Runtime/Tools/MafAgentRuntime.ProjectStructureTools.cs` |
