
# CanDoItAll Canvas Editors — Final Codex bundle

This bundle turns the uploaded raw notes into an implementation-ready, QA-gated Codex work package for the CanDoItAll app.

## What is inside

- `00_INPUTS/` — original user inputs and extracted notes
- `01_ANALYSIS/` — verified current-state findings from the repository
- `02_REQUIREMENTS/` — improved and normalized requirements
- `03_ARCHITECTURE/` — target architecture and architectural decisions
- `04_PLAN/` — implementation ordering and cross-cutting risks
- `05_TRACEABILITY/` — note coverage, manifest, and validation script
- `06_SHARED_PROMPTS/` — master Codex and QA prompts plus screenshot strategy
- `07_ITEMS/` — one implementation subfolder per logical note group
- `08_QA/` — final QA inspector review and validation output

## Bundle counts

- Original non-empty DOCX notes captured: **153**
- Implementation items: **25**
- Required docs per item: **8**

## How to use this bundle

1. Start with `03_ARCHITECTURE/TARGET_ARCHITECTURE.md`.
2. Read `02_REQUIREMENTS/NORMALIZED_DECISIONS.md`.
3. Follow `04_PLAN/IMPLEMENTATION_SEQUENCE.md`.
4. For each item, execute the local item prompt and validation prompt.
5. Do not close any UI-changing item without screenshots and semantic review.

## Hard quality rules

- Every original note remains traceable through `05_TRACEABILITY/traceability_matrix.csv`.
- The Prompt Factory 44-node bug has its own dedicated item and cannot be closed without root-cause proof.
- Screenshot evidence is mandatory for canvas-visible changes.
- Shared architecture comes first: do not duplicate floating tool windows, provider registries, or resource registries.

## Design inspiration

The user-provided Visual Studio Solution Explorer screenshot is copied to:

`00_INPUTS/solution-explorer-reference.png`

Use it as the primary visual inspiration for tree-style floating toolboxes.
