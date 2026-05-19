# Current State

## Runtime Launch

- `ProjectStructureRuntimeLauncher` currently resolves `ProjectObjectType.Script` and `ProjectObjectType.Environment` only.
- Script nodes can launch a configured `Command` plus `Arguments`, or a PowerShell `ScriptPath`, using `WorkingDirectory` when present.
- Environment nodes support .NET runtime, dotnet watch, release run, and Python environment activation.
- Infrastructure docker nodes are available in the catalog as `ProjectObjectType.Infrastructure` with subtype `docker-mode`, but they do not currently resolve as runtime launch nodes.
- Repository local-folder nodes are available as `ProjectObjectType.Repository` with subtype `folder`, but they do not currently resolve as runtime launch working-directory anchors.

## Local Open

- `ProjectStructureLocalFileOpener` only resolves filesystem storage references, managed files, and `artifacts/...` workspace paths.
- It opens directories with `explorer.exe "<path>"` and files with `explorer.exe /select,"<path>"`.
- The request reports Explorer opening at the home path, which is consistent with missing or unresolved local absolute path metadata.
- File nodes can store `ProjectFileMetadata.ExternalPath`, repository nodes can store `LocalPath` and `RelativePath`, and infrastructure deployment-folder nodes can store `FolderPath`, but the opener does not use those metadata fields yet.

## UI Actions

- `ProjectStructurePage.NodeEditing.cs` adds inspector actions for runtime, Explorer open, and IPFS new-tab actions.
- `ProjectStructurePage.razor` passes `CanLaunchRuntimeFromCanvas`, `CanShowLocalOpen`, and `CanOpenIpfsNodeInNewTab` to the graph adapter.
- `ProjectStructureActionCatalogAdapter` can insert `runtime:open`, `runtime:admin`, `open-local`, and `open-new-tab` context menu actions when callers say the action is available.
- Existing component tests cover a .NET runtime action and managed artifact folder action, but not docker, Python launch command details, local absolute folders, local file location, GitHub/GitLab recognition, or agent catalog guidance.

## Agent Tools

- `project_structure_read` mentions actionCapabilities for runtime and open actions.
- `project_structure_node_catalog` returns the UI catalog, aliases, object types, link kinds, and a short guidance list.
- Current catalog guidance covers work tasks, blocks, files, dependency links, and parentNodeKey, but not concrete instructions for runtime scripts, Python/Docker launch nodes, local folders, local-drive files, links, GitHub/GitLab URLs, or metadata JSON examples.

## Existing Test Anchors

- `tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs` has tests for runtime action rendering and artifact folder Explorer action.
- `tests\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs` has tests for action catalog insertion.
- `tests\CanDoItAll.Tests.Unit\ProjectStructureNodeCatalogTests.cs` has a basic catalog coverage test.
