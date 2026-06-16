# SB19 Proof Manifest

## Scope

SB19 rebuilds the Process template library browser over JSON-backed template catalog projections. It covers US-021 through US-023: category/search browsing, overview/Markdown/diagram/JSON/structure previews, and selective process/role/artifact import into the selected definition authoring projection.

## Implementation Artifacts

- repo://src/CanDoItAll.Processes.Projections/ProcessTemplateCatalogProjectionContracts.cs
- repo://src/CanDoItAll.Processes.Projections/ProcessWorkspaceShellProjectionContracts.cs
- repo://src/CanDoItAll.Processes.Templates/ProcessTemplateLibrarySummaries.cs
- repo://src/CanDoItAll.Processes.Templates/ProcessTemplatePackLoader.cs
- repo://src/CanDoItAll.Processes.Application/ProcessTemplateCatalogProjectionService.cs
- repo://src/CanDoItAll.Processes.Application/ProcessWorkspaceShellProjectionService.cs
- repo://src/CanDoItAll.Modules.Processes/Components/LiveProcessesDashboard.razor
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessDefinitionCanvasPanel.razor
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessTemplateLibraryPanel.razor
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor
- repo://src/CanDoItAll.Modules.Processes/Navigation/ProcessesShellNavigationContributor.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessWorkspaceProjectionClient.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs
- repo://src/CanDoItAll.Web/wwwroot/css/output.css

## Test And Validation Artifacts

- bundle://proof/SB19/build-process-module-sb19.txt
- bundle://proof/SB19/build-solution-sb19.txt
- bundle://proof/SB19/build-playwright-project-sb19.txt
- bundle://proof/SB19/test-unit-template-catalog-sb19.txt
- bundle://proof/SB19/test-components-process-shell-sb19.txt
- bundle://proof/SB19/test-playwright-process-shell-sb19.txt
- bundle://proof/SB19/tailwind-build-sb19.txt
- bundle://proof/SB19/source-assertions.txt
- bundle://proof/SB19/semantic-invariants.md
- bundle://proof/SB19/red-team-semantic-proof.md
- bundle://proof/SB19/story-coverage.md
- bundle://proof/SB19/browser-validation.md
- bundle://proof/SB19/ui-parity-repair.md
- bundle://proof/SB19/codeanalytics-snapshot-summary.txt
- bundle://proof/SB19/bundle-validator-prepared-sb19.txt
- bundle://proof/SB19/git-diff-check-sb19.txt
- bundle://proof/SB19/performance-scan-summary.json
- bundle://proof/SB19/scans/projection-boundary-scan.txt
- bundle://proof/SB19/scans/old-symbol-scan.txt
- bundle://proof/SB19/scans/anti-stub-scan.txt
- bundle://proof/SB19/scans/performance-antipattern-scan.txt
- bundle://proof/SB19/changed-file-hashes.txt
- bundle://proof/SB19/line-counts.txt

## Browser Artifacts

- bundle://proof/SB19/browser/browser-validation-summary.txt
- bundle://proof/SB19/browser/processes-template-library-preview.png
- bundle://proof/SB19/browser/processes-template-library-imports.png
- bundle://proof/SB19/browser/processes-definition-step-editor.png
- bundle://proof/SB19/browser/processes-definition-canvas.png
- bundle://proof/SB19/browser/processes-live-dashboard.png
- bundle://proof/SB19/browser/processes-definition-role-editor.png
- bundle://proof/SB19/browser/processes-global-definition-catalog.png
- bundle://proof/SB19/browser/processes-project-shell.png

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative or guard proof |
| --- | --- | --- | --- | --- |
| Template catalog projection | `ProcessTemplateCatalogProjectionService.GetCatalogAsync` | `ProcessWorkspaceShellProjectionService`, `ProcessWorkspaceShell.razor`, and `ProcessTemplateLibraryPanel.razor` | Template pack summaries are converted into bounded process, role, and artifact catalog rows with typed category/search/query state. | `projection-boundary-scan.txt` shows no UI direct file, persistence, or HTTP access. |
| Canonical preview package | `ProcessTemplateLibrarySummaryBuilder` | Catalog preview DTO and template library preview tabs | Canonical JSON is serialized through the source-generated template context; Markdown, Mermaid, and structure tree are generated projections from the same definition. | Unit test `Template_catalog_projection_uses_canonical_json_and_generated_previews` asserts source hash, JSON, Markdown, Mermaid, structure, and target steps. |
| Template import command | `ProcessTemplateLibraryPanel.razor` | Projection client and application catalog service | Process, role, and artifact imports use typed command kind, item key, expected catalog version token, query state, target definition key, and artifact target step when required. | Unit test `Template_catalog_rejects_stale_import_version_tokens` and component tests prove stale/version and typed command boundaries. |
| Imported component projection | `ProcessTemplateCatalogProjectionService.ExecuteCommandAsync` | Template library panel and SB20 exchange/Git UI handoff | Accepted imports record item key, kind, title, source definition key, source component key, canonical source hash, target step, and observed time. | Unit test `Template_catalog_imports_process_role_and_artifact_with_target_validation` rejects missing target steps and asserts source identity/hash on artifact import. |
| Shell template query state | `ProcessWorkspaceShell.razor` | `ProcessWorkspaceShellProjectionService` through `ProcessWorkspaceShellRequest.TemplateCatalogQuery` | Search/category/selected item/preview tab state is carried through refresh without bypassing the projection service. | Component test `Template_library_renders_search_categories_and_preview_tabs` asserts query updates and preview tab rendering. |

## Result

SB19 closure passes after the UI parity repair. Builds, focused tests, Playwright browser proof, refreshed screenshots, and CodeAnalytics MCP all passed. The repair replaces the reshaped custom canvas with the shared CanvasLib/OverlayLib workbench and restores the original dense list/detail process workspace shape while retaining the new projection/versioning contracts.
