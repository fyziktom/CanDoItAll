# SB19 Semantic Invariants

## Invariants

| ID | Invariant | Proof |
| --- | --- | --- |
| SB19-INV-001 | The template library UI renders from `ProcessTemplateCatalogProjection`, not from direct template files, runtime entities, persistence entities, or legacy dialog code. | `projection-boundary-scan.txt`, `old-symbol-scan.txt`, component test `Template_library_renders_search_categories_and_preview_tabs`. |
| SB19-INV-002 | JSON remains the canonical template source; Markdown, Mermaid, and structure are generated projections. | `Template_catalog_projection_uses_canonical_json_and_generated_previews`, `performance-antipattern-scan.txt`, `processes-template-library-preview.png`. |
| SB19-INV-003 | Catalog browsing uses typed category/query/item/preview-tab values and bounded take limits rather than magic command strings. | `ProcessTemplateCatalogProjectionContracts.cs`, `source-assertions.txt`, component query update tests. |
| SB19-INV-004 | Selective imports use typed process/role/artifact command kinds and reject kind mismatches or stale catalog version tokens. | `Template_catalog_imports_process_role_and_artifact_with_target_validation`, `Template_catalog_rejects_stale_import_version_tokens`. |
| SB19-INV-005 | Artifact imports require a target step from the SB18 step editor projection. | Unit target-step rejection proof, component target-step select command assertion, Playwright artifact import action. |
| SB19-INV-006 | Accepted imported component state preserves source definition key, source component key, canonical source hash, and target step for downstream exchange/conflict handling. | Unit import assertion and `ProcessTemplateImportedComponentProjection` contract. |

## Shallow-Pass Trap

A shallow implementation could show static template cards, hardcode Markdown/diagram text, or emit untyped import button clicks without preserving source identity. SB19 rejects that trap by building the catalog from `ProcessTemplatePackLoader`, using source-generated canonical JSON serialization for previews/hashes, asserting typed import commands, validating artifact target steps, rejecting stale version tokens, and preserving source metadata for accepted imports.

## Adversarial Negative Proof

`Template_catalog_imports_process_role_and_artifact_with_target_validation` attempts an artifact import without a target step and receives a rejected receipt. `Template_catalog_rejects_stale_import_version_tokens` submits a command with an outdated version token and receives a rejected receipt without advancing the catalog import revision.

## Semantic Positive Proof

Focused unit tests load a realistic template pack and verify process/role/artifact category counts, canonical JSON, source hash, generated Markdown, generated Mermaid, structure nodes, import target steps, accepted process/role/artifact receipts, imported component source metadata, and stale-token rejection. Component tests verify search/category/preview tab rendering and import command payloads. Playwright loads `/processes`, searches the library, selects a process template, exercises Markdown/diagram/JSON/structure tabs, imports process/role/artifact components, selects an artifact target step, captures screenshots, and records browser console/network summary.

## Production Behavior Artifact Matrix

| Production artifact | Producer | Consumer | Lifecycle | Proof citation |
| --- | --- | --- | --- | --- |
| `ProcessTemplateCatalogProjection` | Application template catalog projection service | Process shell and template library panel | Created from template pack definitions and refreshed after query/import commands. | `test-unit-template-catalog-sb19.txt`, `test-components-process-shell-sb19.txt`. |
| `ProcessTemplateCatalogPreviewProjection` | Template library summary builder and catalog service | Preview tabs for overview, Markdown, diagram, JSON, and structure | Source JSON, hash, generated Markdown/Mermaid, and structure tree are carried together. | `processes-template-library-preview.png`, unit preview assertions. |
| `ProcessTemplateImportCommand` | Template library panel | Projection client and application service | Created from typed UI state with expected version token and optional target step. | Component import command tests. |
| `ProcessTemplateImportCommandReceipt` | Catalog service | Shell state and receipt UI | Accepted/rejected import outcome is rendered and returned with an updated projection. | Playwright import receipt assertions. |
| `ProcessTemplateImportedComponentProjection` | Catalog service | Template library panel and SB20/SB27 downstream work | Accepted imports retain source identity, canonical hash, target step, and import time. | Unit source identity/hash assertion and `source-assertions.txt`. |
