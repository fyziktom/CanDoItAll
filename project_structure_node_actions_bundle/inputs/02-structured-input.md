# Structured Input

## Objectives

- Runtime-capable project-structure nodes expose two run actions from the double-click quick-action modal: normal run and administrator run.
- Runtime-capable project-structure nodes expose the same two run actions from the canvas right-click context menu.
- File-backed project-structure nodes expose "Open in File Explorer" when the file is trusted and available on the local drive or managed file roots.
- IPFS-backed file nodes expose "Open in New Tab" instead of local File Explorer launch.
- Project Structure MCP responses and internal agent project-structure tools include enough node capability information for agents to know which runtime/file/IPFS actions are available and how those actions work.

## Hard Constraints

- Do not bypass `IWorkspacePathAccessGuard` for local file paths.
- Do not execute arbitrary commands from file nodes; local file opening remains limited to trusted managed paths and existing blocked extension rules.
- Do not create a new canvas menu system when `CanvasWorkbenchAction` and `ContextMenuHost` already own the right-click menu.
- Do not treat browser proof as host proof for PowerShell, UAC, or File Explorer. Record any host-proof limit explicitly.
- Do not silently narrow "always two options" for runtime nodes: when a runtime launch plan resolves and the host supports launch, both normal and administrator actions must be present.

## Assumptions

- "Runtime nodes" are nodes whose metadata resolves through `IProjectStructureRuntimeLauncher.Resolve`, currently `Script` and supported `Environment` nodes.
- The "modal that opens after doubleclick" is `ProjectStructureQuickActionDialogState`, opened through `OpenQuickActionDialog`.
- "Right click menu" is the CanvasLib node context menu populated by `ProjectStructureActionCatalogAdapter.BuildNodeContextActions`.
- "File related nodes" are nodes with managed file or media routes, storage references, media metadata, or artifact routes.
- "On IPFS" is detectible from `StorageObjectReference.ProviderKind == StorageProviderKind.Ipfs`, IPFS storage access metadata, or an absolute IPFS-backed route.

## Validation Expectations

- Component or unit tests prove action lists for runtime, local file, and IPFS nodes.
- Build or targeted tests prove the contract changes compile across Workbench, MCP, and internal agent tools.
- Browser proof opens the project structure route, double-clicks or otherwise opens the quick-action dialog, opens a node context menu, and verifies the visible actions and layering.
- Host proof is attempted or documented for PowerShell/File Explorer launch surfaces; UI proof must still verify the actions are presented.
