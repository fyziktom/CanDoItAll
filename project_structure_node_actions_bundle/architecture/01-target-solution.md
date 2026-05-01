# Target Solution

## Core Shape

Add a shared Workbench node action capability model that describes whether a project-structure node can:

- run a resolved runtime command normally
- run a resolved runtime command as administrator
- open a trusted local file or folder in File Explorer
- open an IPFS or route-backed file in a new browser tab

The UI, Project Structure MCP, and internal agent tools should consume that capability model instead of independently guessing from object type or raw metadata.

## UI Ownership

- `ProjectStructurePage.NodeQuickActions.cs` owns the double-click modal state and execution.
- `ProjectStructureActionCatalogAdapter.cs` owns canvas right-click menu actions.
- `ProjectStructurePage.NodeEditing.cs` owns inspector/support-panel actions and action dispatch.
- `ProjectStructurePage.razor` owns modal rendering and local attachment preview wiring.
- `ProjectStructureMenuComposition.cs` owns first-ring context-menu ordering.

## Host Action Ownership

- Runtime command execution remains in `IProjectStructureRuntimeLauncher`.
- Local file opening remains in `IProjectStructureLocalFileOpener`.
- Browser new-tab opening remains in `OpenArtifactInNewTabAsync` through JS interop.

## Contract Ownership

- `ProjectStructureAgentContracts.cs` defines the agent/MCP-facing node summary shape.
- `ProjectStructureAgentService.MapNodeSummary` computes capability metadata for `project_structure_read`.
- `ProjectStructureTools.cs` documents MCP behavior.
- `MafAgentRuntime.ProjectStructureTools.cs` maps the same capability metadata into internal agent compact nodes and documents behavior for internal tools.

## Recommended Model

Create a small structured payload such as `ProjectStructureNodeActionCapabilities` with booleans and optional descriptive fields:

- `CanRunNormally`
- `CanRunAsAdministrator`
- `RuntimeDisplayName`
- `RuntimeDisplayCommand`
- `RuntimeWorkingDirectory`
- `CanOpenInFileExplorer`
- `CanOpenInNewTab`
- `OpenInNewTabRoute`
- `StorageProvider`
- `StorageLocatorKind`
- `ActionNotes`

Names can adjust to local style, but the model must stay explicit enough that agents do not parse raw `MetadataJson` or storage JSON.

## Safety Boundaries

- The payload is informational and UI-guidance oriented.
- Runtime launch and local open remain guarded by their existing services at execution time.
- IPFS opens are route/new-tab opens only.
- Blocked executable extensions remain blocked for File Explorer launch.
