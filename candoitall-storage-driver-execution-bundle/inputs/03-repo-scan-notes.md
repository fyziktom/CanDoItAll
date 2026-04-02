
# Repo Scan Notes

## High-signal findings

- `WorkspaceStorage.cs` currently exposes `IFileStore` / `IManagedArtifactStore` with local path semantics only.
- `ProjectWorkbenchModels.cs` and `PromptFactoryService.Pack.cs` are the main persisted upload writers today.
- `ManagedFilesEndpointRoutes.cs` currently serves only active-profile filesystem assets.
- `DatabaseSnapshots.cs` contains ad hoc local/IPFS transport logic and is the clearest existing bulk-transfer candidate.
- `ProjectWorkbenchMetadata.cs` already contains file subtype inference and a `Recording.StorageReference` field; both are valuable for routing and storage-node design.
- `SettingsPage.razor` has no storage tab yet, but `DatabaseSourcesSettingsPanel.razor` is a strong pattern source for list/detail/test-connection flows.
- `ProjectStructureSelectionPanel.razor`, `ProjectStructurePage.razor`, and `ProjectStructureCanvasDialogs.razor` are the main preview/open UI surfaces that must stop assuming local relative paths.
- `tests/CanDoItAll.Tests.Playwright/ProjectStructureArtifactBrowserTests.cs` already captures rich file-preview evidence and should be extended rather than bypassed.
- `tests/CanDoItAll.Tests.Support/FakeIpfsTestServer.cs` provides an existing IPFS test seam; no equivalent FTP proof harness exists yet.

## Scope split used in this bundle

- In scope: storage catalog, routing rules, provider runtime, upload/export adoption, unified access logic, storage settings UI, project-structure storage nodes, tests, Playwright MCP proof.
- Adjacent but inventoried: resources-module FTP editor patterns, `MainLayout.razor` transient tuning attachments, runtime launcher safety review, MCP SSH/SFTP transport as implementation inspiration only.
- Out of immediate implementation scope unless product direction changes: converting every internal repo-local file read (prompt library pack loader, MCP registries, watch/runtime internals) into storage-driver traffic.

## Why the XLSX inventory exists

- The request used “all uploads and views/downloads/use of files”.
- The repo already has multiple persisted and non-persisted file surfaces spread across modules.
- The workbook makes those surfaces explicit and ties each to a workstream, checklist, and proof route.
