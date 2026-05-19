# Requirement Traceability

| Input or requirement | Bundle location | Owning subbundle | Planned proof | Notes |
| --- | --- | --- | --- | --- |
| Use the bundle workflow. | `inputs/`, `requirements/`, `plan/`, `subbundles/`, `reviews/` | `01-current-implementation-audit-and-stage-truth` | Bundle validator prepared/completed pass. | Required by explicit user request. |
| Analyze actual Cognitive Memory stage. | `analysis/01-current-state.md`, `docs/cognitive-memory/current-state/stage-assessment.md` | `01-current-implementation-audit-and-stage-truth` | Source references and stage matrix present. | Stage set to validation-grade alpha. |
| Create dedicated docs folder with subfolders. | `docs/cognitive-memory` | `02-documentation-section-and-mermaid-diagrams` | Folder and docs files exist. | Covers current state, architecture, operations, roadmap. |
| Add Mermaid class, sequence, flow, and architecture-beta graphs. | `docs/cognitive-memory/architecture/*.md`, `docs/cognitive-memory/current-state/implementation-map.md` | `02-documentation-section-and-mermaid-diagrams` | Mermaid blocks present in docs. | Diagrams describe current implementation, not future wish list. |
| Add roadmap of done and next work. | `docs/cognitive-memory/roadmap/roadmap.md` | `03-roadmap-and-closure-validation` | Roadmap includes already done, P0/P1/P2, and beta gates. | Roadmap calls out alpha refactors and hardening. |
| Improve existing docs entry points. | `README.md`, `architecture/README.md`, `docs/README.md`, `docs/api-control-plane.md`, `docs/architecture-beta.md`, `docs/cognitive-memory-api.md` | `03-roadmap-and-closure-validation` | Updated docs reference the new section. | Old API doc retained as compatibility pointer. |
| Validate closure. | `reviews/01-execution-report.md` | `03-roadmap-and-closure-validation` | Bundle validators and `git diff --check`. | No runtime tests required for docs-only edits. |
