# Source Artifacts

## Raw Inputs

- `inputs/00-original-request.md`: user request received on 2026-05-19.

## Repo Evidence

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureRuntimeLauncher.cs`: current PowerShell launch resolver supports Script and Environment nodes, but not docker infrastructure or repository folder nodes.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureLocalFileOpener.cs`: current Explorer opener resolves only managed files and managed artifact roots, so user-selected absolute folder or file paths are not covered.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCanvasCatalog.RichDefinitions.cs`: create catalog already has runtime, repository folder, infrastructure docker, deployment folder, and file definitions, but agent guidance and typed actions need tightening.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCanvasCatalog.cs`: agent-facing node catalog guidance is generated here.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureNodeActionCapabilityResolver.cs`: actionCapabilities exposed to agents are generated here.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureNodeKindRequestJsonConverters.cs`: object type and subtype aliases for agent create/update payloads are normalized here.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Workbench\ProjectWorkbenchMetadata.cs`: metadata model for repositories, scripts, environments, infrastructure folders, links, and files.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCreateRequestComposer.cs`: UI create dialogs map user fields into metadata.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureNodeEditor.cs`: edit dialogs map changed fields into metadata.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.razor`: canvas wiring and CanShowLocalOpen checks.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\Pages\ProjectStructurePage.NodeEditing.cs`: inspector actions dispatch runtime and local-open actions.
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\CanvasAdapters\ProjectStructureActionCatalogAdapter.cs`: node context menu actions.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructurePageTests.cs`: existing component tests for runtime actions, artifact folders, compact path presentation, and editing.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Components\ProjectStructureActionCatalogAdapterTests.cs`: existing action catalog tests.
- `C:\repositories\CanDoItAll\tests\CanDoItAll.Tests.Unit\ProjectStructureNodeCatalogTests.cs`: existing agent catalog test.

## Validation Artifacts To Produce

- Targeted .NET test output for component and unit tests touching project structure.
- Playwright MCP screenshot evidence for creating/selecting runtime, folder/file, repository, and link nodes, with open action dialogs visible.
- Host-level validation note for PowerShell and Explorer launch behavior, or an explicit blocker if UAC or shell windows cannot be captured safely.
