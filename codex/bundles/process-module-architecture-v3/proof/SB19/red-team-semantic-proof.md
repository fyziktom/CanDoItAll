# SB19 Red-Team Semantic Proof

## Attack Model

SB19 would be invalid if it only recreated the old visual dialog while still treating Markdown/Mermaid as canonical, bypassing migrated JSON templates, emitting stringly typed import commands, or allowing artifact imports without a selected target step.

## Red-Team Checks

| Check | Result | Evidence |
| --- | --- | --- |
| Search/category UI is backed by projection query state, not static cards. | Passed | Component test `Template_library_renders_search_categories_and_preview_tabs` and `ProcessWorkspaceShellRequest.TemplateCatalogQuery`. |
| Preview tabs use generated projections over canonical JSON. | Passed | Unit test `Template_catalog_projection_uses_canonical_json_and_generated_previews`; screenshot `processes-template-library-preview.png`. |
| Process/role/artifact import commands are typed. | Passed | `ProcessTemplateImportCommandKind`, component command capture, unit accepted import tests. |
| Artifact import cannot silently attach to a missing step. | Passed | Unit rejected artifact import without target step; Playwright selects `processes-template-library-artifact-target` before artifact import. |
| Stale catalog versions fail predictably. | Passed | Unit stale-token rejection proof. |
| Legacy template dialog symbols were not revived. | Passed | `scans/old-symbol-scan.txt`. |
| Stub or placeholder implementations were not introduced in production code. | Passed | `scans/anti-stub-scan.txt`. |

## Residual Risk

`ProcessTemplateCatalogProjectionService` is a large cohesive service because SB19 owns both catalog projection construction and typed import command acceptance. The service does not cross persistence/runtime boundaries and focused tests cover command semantics, but SB28 should revisit splitting or extracting helpers if the catalog grows beyond browsing/import responsibilities.
