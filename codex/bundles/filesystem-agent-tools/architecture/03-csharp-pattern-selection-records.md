# C# Pattern Selection Records

## PSR-001 Filesystem Runtime Plugin

- Problem force: `WorkspaceRuntimePlugin` mixes unrelated runtime tool domains and grows whenever a filesystem command is added.
- Rejected simpler option: add new `HashPath`, `ZipPath`, and `UnzipArchive` methods directly to `WorkspaceRuntimePlugin`; this would deepen the hotspot identified by CodeAnalytics.
- Selected pattern: cohesive adapter/facade plugin.
- New type: `WorkspaceFilesystemRuntimePlugin`.
- Dependency direction: MAF adapter depends on `IWorkspaceFileService`; file service remains in Core.
- Test seam: instantiate the plugin directly with `WorkspaceFileService` and access settings.
- Proof: tests call extracted plugin without constructing `MafAgentRuntime`; source assertion confirms old runtime no longer owns file methods.

## PSR-002 Catalog Constants And Capability Registry

- Problem force: tool names are stringly repeated across policy, templates, and builder code.
- Rejected simpler option: add only template JSON rows; runtime policy would not know the tools.
- Selected pattern: catalog constants with policy registry entries.
- New/changed types: `ToolContractCatalog`, `ToolCapabilityRegistry`, capability templates.
- Test seam: unit tests assert new tool names are known, classified, and approval-protected as appropriate.

## PSR-003 Thin Composition Wiring

- Problem force: construction must create the filesystem plugin without making the composition root own business logic.
- Rejected simpler option: use service location inside tool execution methods.
- Selected pattern: explicit constructor injection into `ToolCapabilityBuilder` and `ConfiguredWorkspaceToolSet`.
- Test seam: composition test verifies tools attach through existing capability flow.
