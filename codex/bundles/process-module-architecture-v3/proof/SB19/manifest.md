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
- repo://src/CanDoItAll.Modules.Processes/Components/ProcessWorkspaceShell.razor.css
- repo://src/CanDoItAll.Modules.Processes/Navigation/ProcessesShellNavigationContributor.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessWorkspaceProjectionClient.cs
- repo://src/CanDoItAll.Modules.Processes/Services/ProcessesModuleServiceCollectionExtensions.cs
- repo://src/CanDoItAll.Web/Composition/ShellNavigation.cs
- repo://tests/CanDoItAll.Tests.Components/ProcessWorkspaceShellTests.cs
- repo://tests/CanDoItAll.Tests.Components/ShellNavigationContributionTests.cs
- repo://tests/CanDoItAll.Tests.Playwright/ProcessShellSmokeTests.cs
- repo://src/CanDoItAll.Web/wwwroot/css/output.css

## Test And Validation Artifacts

- bundle://proof/SB19/build-process-module-sb19.txt
- bundle://proof/SB19/build-solution-sb19.txt
- bundle://proof/SB19/build-playwright-project-sb19.txt
- bundle://proof/SB19/build-web-ui-parity-tabs-repair.txt
- bundle://proof/SB19/test-unit-template-catalog-sb19.txt
- bundle://proof/SB19/test-components-process-shell-sb19.txt
- bundle://proof/SB19/test-components-process-shell-tabs-repair.txt
- bundle://proof/SB19/test-playwright-process-shell-sb19.txt
- bundle://proof/SB19/test-playwright-process-shell-tabs-repair.txt
- bundle://proof/SB19/tailwind-build-sb19.txt
- bundle://proof/SB19/source-assertions.txt
- bundle://proof/SB19/semantic-invariants.md
- bundle://proof/SB19/red-team-semantic-proof.md
- bundle://proof/SB19/story-coverage.md
- bundle://proof/SB19/browser-validation.md
- bundle://proof/SB19/ui-parity-repair.md
- bundle://proof/SB19/ui-parity-tabs-repair.md
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
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-tabs-tree-definition.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-steps-canvas-floating-windows.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-graphs-cost-token-time.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-manager-chat.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-live-processes-menu-order.png
- bundle://proof/SB19/ui-parity-tabs-repair/browser/after-live-processes-subpage.png

## Production Behavior Artifact Matrix

| Signal | Producer | Consumer | Lifecycle | Negative or guard proof |
| --- | --- | --- | --- | --- |
| Template catalog projection | `ProcessTemplateCatalogProjectionService.GetCatalogAsync` | `ProcessWorkspaceShellProjectionService`, `ProcessWorkspaceShell.razor`, and `ProcessTemplateLibraryPanel.razor` | Template pack summaries are converted into bounded process, role, and artifact catalog rows with typed category/search/query state. | `projection-boundary-scan.txt` shows no UI direct file, persistence, or HTTP access. |
| Canonical preview package | `ProcessTemplateLibrarySummaryBuilder` | Catalog preview DTO and template library preview tabs | Canonical JSON is serialized through the source-generated template context; Markdown, Mermaid, and structure tree are generated projections from the same definition. | Unit test `Template_catalog_projection_uses_canonical_json_and_generated_previews` asserts source hash, JSON, Markdown, Mermaid, structure, and target steps. |
| Template import command | `ProcessTemplateLibraryPanel.razor` | Projection client and application catalog service | Process, role, and artifact imports use typed command kind, item key, expected catalog version token, query state, target definition key, and artifact target step when required. | Unit test `Template_catalog_rejects_stale_import_version_tokens` and component tests prove stale/version and typed command boundaries. |
| Imported component projection | `ProcessTemplateCatalogProjectionService.ExecuteCommandAsync` | Template library panel and SB20 exchange/Git UI handoff | Accepted imports record item key, kind, title, source definition key, source component key, canonical source hash, target step, and observed time. | Unit test `Template_catalog_imports_process_role_and_artifact_with_target_validation` rejects missing target steps and asserts source identity/hash on artifact import. |
| Shell template query state | `ProcessWorkspaceShell.razor` | `ProcessWorkspaceShellProjectionService` through `ProcessWorkspaceShellRequest.TemplateCatalogQuery` | Search/category/selected item/preview tab state is carried through refresh without bypassing the projection service. | Component test `Template_library_renders_search_categories_and_preview_tabs` asserts query updates and preview tab rendering. |
| Process workspace tab state | `ProcessWorkspaceShell.razor` | Detail tab renderers for definition, roles, steps, runs, graphs, analytics, exchange, and manager chat | Selected definition data is split into original-style process detail tabs instead of vertically stacking unrelated panels. | Component test `Original_process_workspace_tabs_render_runs_graphs_analytics_and_manager_chat` and Playwright artifacts under `ui-parity-tabs-repair/browser`. |
| Live process navigation child | `ProcessesShellNavigationContributor` and `ShellNavigation.BuildItems` | App shell navigation and `/processes/live` page | Contributed child routes can attach to contributed parents, so Live Processes renders immediately after Processes and opens the live dashboard. | Component test `Process_contribution_inserts_live_processes_after_contributed_process_parent` and browser artifact `after-live-processes-menu-order.png`. |

## Result

SB19 closure passes after the UI parity tab/tree/live-navigation repair. Builds, focused tests, Playwright browser proof, refreshed screenshots, and CodeAnalytics MCP all passed. The repair keeps the shared CanvasLib/OverlayLib workbench, restores the original dense tree/list plus tabbed process detail shape, and keeps the new projection/versioning contracts.
