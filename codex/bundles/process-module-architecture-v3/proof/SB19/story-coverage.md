# SB19 Story Coverage

| Story or criterion | Status | Proof |
| --- | --- | --- |
| US-021: browse a template catalog by category and search term. | Covered | `Template_library_renders_search_categories_and_preview_tabs`; Playwright search for `AI-assisted` and category `Processes`; `processes-template-library-preview.png`. |
| US-022: preview overview, Markdown, diagrams, JSON, and structure tree. | Covered | Unit generated preview assertions; component preview tab assertions; Playwright Markdown, diagram, JSON, and structure tab actions. |
| US-023: import a full process template or selectively add related role/artifact components. | Covered | Unit accepted process/role/artifact imports; component import command capture; Playwright process, role, and artifact import receipts. |
| AC-022: JSON is canonical template source. | Covered | Canonical JSON preview and source hash assertions in `test-unit-template-catalog-sb19.txt`. |
| AC-023: Markdown and Mermaid are generated projections. | Covered | `GeneratedProjectionNotice`, generated Markdown/Mermaid fields, and preview tab proof. |
| AC-024: global components and local import identity are represented. | Covered | Imported component projections carry source definition key, source component key, canonical source hash, kind, item key, and target step. SB20 owns Git conflict UI. |
| AC-025: versioning and migration status remain visible. | Covered | Template catalog projection exposes pack version and catalog version tokens; stale version import rejection is tested. |
| AC-026: Git wrapper and generic Git UI stay out of Process template browser. | Covered | SB19 does not add Git UI or merge/conflict logic; SB20 remains the owner. |
| AC-040: browser-facing story proof is captured. | Covered | `test-playwright-process-shell-sb19.txt`, `browser-validation.md`, and screenshots under `proof/SB19/browser/`. |
