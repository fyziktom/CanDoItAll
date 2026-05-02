# Current State

## Agent Project-Structure Access

- Internal technical agents already have project-structure tools attached by default when workspace services are available.
- Per-agent project-structure access lives in `AgentProjectStructureAccessSettings` and is serialized under the `projectStructure` key in agent `ConfigurationJson`.
- The technical agent editor renders read/write and allowed-project controls in `AgentCatalogPanel`.

Source references:

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Models\Agents\Access\AgentProjectStructureAccessModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Catalog\AgentFrameworkWorkspaceCatalogService.Agents.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Tools\MafAgentRuntime.ProjectStructureTools.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.AgentFramework\Pages\Components\AgentCatalogPanel.razor.cs`

## External Workspace Paths

- Workspace path policy already supports mapped aliases like `external-target/C/path/to/repo`.
- Workspace file query/mutation and dotnet command tools can resolve these aliases.
- Governed process runs already carry `agentAllowedExternalTargetAliases` and `agentReadOnlyExternalTargetAliases` metadata, but ordinary technical-agent settings do not expose a per-agent external workspace allowlist.

Source references:

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Paths\WorkspacePathPolicy.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Execution\ExecutionInvocationMetadata.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\ToolPolicy\AgentToolInvocationPolicy.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Core\Workspace\Files\WorkspaceFileService.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Workspace\MafAgentRuntime.WorkspaceRuntimePlugin.cs`

## File Tools

- Workspace file capabilities exist for list, search, read, stat, diff, git status, git diff, and mutations.
- Seeded agents already receive browse/search/read capabilities in many templates.
- There is no agent-settings section that says which external workspace roots the tools may touch outside governed process-run metadata.

Source references:

- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Persistence\Seeds\SandboxWorkspaceSeedBuilder.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.AgentFramework.Maf\Runtime\Capabilities\MafAgentRuntime.Capabilities.Tools.cs`

## Mermaid And File Nodes

- Mermaid nodes are modeled as `ProjectObjectType.File` with `objectSubtype = "mermaid"`; diagram type is detected from node notes.
- ProjectStructure MCP and internal tools currently describe typed block variants, but they do not plainly say that Mermaid outputs must be created as typed file asset nodes.
- The Workbench UI catalog already has an `add-file-mermaid` leaf action.

Source references:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectNodes\ProjectNodeKindRegistry.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureCreateRequestComposer.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workbench\ProjectStructure\ProjectStructureAgentContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.ProjectStructure\ProjectStructureTools.cs`

## Storage Drivers

- Storage catalog and driver infrastructure exists with filesystem, IPFS, and FTP drivers.
- Driver contract supports save, read, delete, and connection tests, but not provider-independent directory listing.
- Agent runtime does not currently expose storage-driver tools as a default internal tool family.

Source references:

- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Abstractions\StorageContracts.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Drivers\FileSystemStorageDriver.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Infrastructure\Storage\Persistence\StoragePersistenceModels.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Storage\WorkspaceService.Storage.cs`
- `C:\repositories\CanDoItAll\src\CanDoItAll.Modules.Workspace\Storage\WorkspaceStorageModels.cs`
