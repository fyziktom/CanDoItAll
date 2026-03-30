# Normalized Requirements

- `R01`: The active shared workbench stage must render frames, links, nodes, and minimap through canvas-owned runtime surfaces.
- `R02`: Canvas export must compose renderer-owned canvas layers directly and keep accessibility mirror behavior intact.
- `R03`: ProjectStructure move handling must patch committed node positions without unconditional full-surface reload.
- `R04`: ProjectStructure and PromptFactory canvas UI state persistence must use delayed write-behind for drag and state-change flows.
- `R05`: CanvasLib assets must be centralized and consumed through shared include components instead of duplicated shell script lists.
- `R06`: PromptFactory and sandbox benchmark surfaces must remain compatible with the shared renderer after the migration.
- `R07`: Dead or misleading legacy runtime paths must be reduced, and any remaining compatibility surfaces must be explicitly non-runtime.
- `R08`: Closure requires green asset verification, green component tests, green Playwright tests, archived browser evidence, and a passing bundle validator gate.
