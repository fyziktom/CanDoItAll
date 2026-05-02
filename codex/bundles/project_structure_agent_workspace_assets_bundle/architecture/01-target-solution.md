# Target Solution

## Architecture

Add a new agent workspace/storage access model alongside existing project-structure and process access metadata. The model is stored in agent `ConfigurationJson`, appears in the technical agent editor, and is read by the MAF runtime when composing built-in workspace and storage tools.

The runtime keeps using the existing `WorkspacePathPolicy` external alias convention. User-entered absolute paths are normalized at save time to aliases such as `external-target/C/repositories/SomeRepo`. Runtime guards validate every external path argument against the agent's configured aliases before delegating to `IWorkspaceFileService` or `IWorkspaceCommandExecutionService`.

Storage tools are implemented as an internal runtime plugin backed by `IStorageCatalogService` and `IStorageDriverRegistry`. The first tool set exposes catalog list, text read, text write, and delete. The tools deny access when the agent lacks permission, when the storage catalog is not allowed, when storage is disabled/read-only, or when the driver lacks the needed capability.

Project-structure Mermaid/file guidance is strengthened in both the internal tool builder and the external MCP tool descriptions. The canonical contract is:

- Mermaid diagram: `objectType = File`, `objectSubtype = "mermaid"`, Mermaid source in `notes`, omit metadata unless needed so diagram kind can auto-detect.
- Other generated files: `objectType = File`, file-specific `objectSubtype` such as `markdown`, `json`, `text`, `log`, `pdf`, `docx`, `excel`, or `screenshot`, and media payload when the file bytes need to be attached.

## Boundaries

- Do not weaken project/process access semantics.
- Do not grant broad external-drive access by default.
- Do not create a second storage abstraction; use existing storage infrastructure.
- Do not require a browser-only proof for non-visual runtime behavior.
