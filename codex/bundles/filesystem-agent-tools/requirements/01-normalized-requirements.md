# Normalized Requirements

| ID | Requirement | Acceptance |
|---|---|---|
| FS-001 | Agents can list a folder without automatically recursing through every subfolder. | A filesystem tool returns immediate children when requested and records a workspace receipt. |
| FS-002 | Agents can list folders recursively when needed. | Recursive listing remains available with explicit bounded parameters and truncation. |
| FS-003 | Agents can copy folders and create folders through clear tool names/descriptions. | Tool metadata/descriptions explicitly state directory support and existing behavior remains routed through `IWorkspaceFileService`. |
| FS-004 | Agents can compute hashes and archive/extract files or folders. | `HashPath`, `ZipPath`, and `UnzipArchive` become agent-visible tools with classifications and template capabilities. |
| FS-005 | Allowed-area checks remain authoritative. | Runtime plugin methods call workspace file service after the existing MAF access checks; tests include denial/classification proof. |
| FS-006 | Filesystem tool behavior is no longer owned by the broad `WorkspaceRuntimePlugin`. | A top-level filesystem plugin owns file operations; `WorkspaceRuntimePlugin` no longer exposes the filesystem method family. |
| FS-007 | The tool catalog is easier to discover. | Constants/capability template entries/tool descriptions are updated for the filesystem family and tests assert new tool names are known. |
| FS-008 | The change is testable without constructing `MafAgentRuntime`. | Unit tests instantiate the extracted filesystem plugin or file service directly. |
