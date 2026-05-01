# Current State Analysis

## Runtime Actions

- `IProjectStructureRuntimeLauncher` already resolves and launches runtime-capable nodes through guarded PowerShell plans. It supports normal and administrator launch through `LaunchAsync(ProjectStructureNode node, bool runAsAdministrator, ...)`.
- `ProjectStructurePage.RuntimeLaunch.cs` already routes launch requests to `RuntimeLauncher.LaunchAsync` and updates workflow feedback.
- `ProjectStructurePage.NodeEditing.cs` already exposes `runtime:open` and `runtime:admin` in inspector actions when `RuntimeLauncher.Resolve(node)` succeeds.
- `ProjectStructurePage.NodeQuickActions.cs` currently resolves a single primary quick action for runtime nodes: "Run PowerShell" using `runtime:open`.
- `ProjectStructureActionCatalogAdapter.BuildNodeContextActions` currently does not add runtime actions to the right-click menu, so context menu users cannot run runtime nodes from there.
- `ProjectStructureMenuComposition.ResolvePrimaryRingActionId` does not prioritize runtime action ids because they are not in the catalog yet.

## File And IPFS Actions

- `ProjectStructureLocalFileOpener` already validates trusted managed/local file paths and opens File Explorer on Windows.
- `ProjectStructurePage.razor` already wires attachment preview and local open actions into selection and attachment preview UI.
- `CanShowLocalOpen` delegates to `LocalFileOpener.IsAvailable && LocalFileOpener.CanOpen(node)`.
- `StorageObjectReference` includes `ProviderKind`, `LocatorKind`, `Locator`, and `Route`, which can identify IPFS references.
- `ManagedFilesEndpointRoutes` supports IPFS references when an absolute route exists or a catalog record can resolve the reference.
- The double-click path currently opens attachment preview directly for previewable attachments and only opens the quick-action modal for non-preview nodes. That must be adjusted so file-related nodes can receive the requested action offer.

## MCP And Internal Agent Tools

- `ProjectStructureAgentContracts.ProjectStructureNodeSummary` currently includes route and optional asset fields but no explicit action-capability payload.
- `ProjectStructureAgentService.MapNodeSummary` controls what `project_structure_read` and agent APIs return.
- `CanDoItAll.Mcp.ProjectStructure.ProjectStructureTools` exposes `project_structure_read` with a compact description that does not mention runtime/file/IPFS capabilities.
- `MafAgentRuntime.ProjectStructureTools.ProjectStructureCompactNode` maps route, notes, metadata, media fields, and layout, but no explicit action capability model.

## Tests Nearby

- `tests/CanDoItAll.Tests.Components/ProjectStructureActionCatalogAdapterTests.cs` covers context action ordering and catalog behavior.
- `tests/CanDoItAll.Tests.Components/ProjectStructurePageTests.cs` covers page-level project-structure behavior and can be extended for quick-action state if needed.
- `tests/CanDoItAll.Tests.Unit/ProjectStructureRuntimeLauncherTests.cs` and `ProjectStructureRuntimeLauncherPathResolverTests.cs` cover launch resolution and path safety.
- `tests/CanDoItAll.Tests.Unit/ProjectStructureLocalFileOpenerManagedFilesTests.cs` covers local file open safety.
- `tests/CanDoItAll.Mcp.ProjectStructure.Tests/ProjectStructureToolsTests.cs` and integration tests cover MCP envelope behavior.

## Discovery Notes

- CodeAnalytics snapshot used for source inventory: `snap-20260430233239-37492784`.
- Components MCP identified `ContextMenuHost` and `ContextMenu` as the relevant shared menu surfaces.
