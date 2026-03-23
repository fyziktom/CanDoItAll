# Bundle Structure

## Purpose

This bundle is organized so that a Codex agent can start at the top, understand the architecture, and then work on one component or one migration wave without having to rediscover the repository structure.

## Top-level documents

- `01_ANALYSIS_CURRENT_STATE.md` — evidence-backed analysis of the relevant canvas surfaces, current reuse, duplication, and architectural problems.
- `02_TARGET_ARCHITECTURE.md` — target framework architecture, subsystem boundaries, performance strategy, and extension model.
- `03_COMPONENT_INVENTORY.md` — master inventory of all proposed components with status and priority.
- `04_IMPLEMENTATION_ROADMAP.md` — recommended delivery sequence across waves.
- `05_FILE_REFERENCE_CATALOG.md` — concrete repository navigation guide with reasons and key symbols.
- `06_QA_UX_ARCHITECTURE_REVIEW.md` — hard-nosed quality review of the whole bundle.
- `07_FUTURE_FEATURE_SIMULATION.md` — validation against four realistic future features.
- `08_EXECUTIVE_SUMMARY.md` — concise leadership summary of findings, critical problems, refactors, and completion status.
- `09_KONVA_REFERENCE_EXTRACTION.md` — Konva-inspired architectural lessons and the local clone files that Codex should inspect for implementation inspiration.

## Components folder

Each component folder includes:

- `README.md` — overview, architectural context, relevant existing files, and implementation approach.
- `SPECIFICATION.md` — detailed component specification with states, edge cases, API proposal, implementation notes, and validation ideas.
- `IMPLEMENTATION_PROMPT.md` — direct Codex implementation prompt with scope boundaries and anti-duplication instructions.
- `VALIDATION_PROMPT.md` — reviewer prompt focused on function, architecture, UX/UI, performance, and clean integration.
- `CHECKLIST.md` — implementation, validation, UX/UI, architecture, and performance checklists.
- `FILE_REFERENCES.md` — real-file navigation guide for that component.
- `DALL_E_PROMPTS.md` — style-specific prompts for design ideation.
- `generate_design_variants.py` — executable Python script prepared for Windows with `OPENAI_API_KEY` and the OpenAI Images API.

## Integration folder

The `integration/` folder contains the cross-component guidance that prevents Codex from creating duplicate abstractions or missing migration seams. Use it whenever work touches existing pages, wrappers, services, or JS runtime files.

## Ordering discipline

The intended reading/implementation order is:

1. Read the analysis and architecture documents.
2. Start with Wave 1 and Wave 2 shared foundations before touching page-level migration.
3. Move Project Structure page logic into domain adapters.
4. Move Prompt Factory page graph logic and history into domain adapters/infrastructure.
5. Migrate Project Calendar to the shared CanvasCalendar wrapper.
6. Add advanced overlays, snapping, diagnostics, clipboard, minimap, and recommendation features only after the base seams exist.

## Completion criterion used by this bundle

This bundle assumes the work is only complete when:

- All required shared foundations are present.
- Legacy wrappers are either retired or explicitly marked as temporary compatibility shims.
- Project Structure and Prompt Factory no longer own graph-projection logic inside page files.
- Project Calendar uses the shared `CanvasCalendar` wrapper through a domain adapter.
- QA, UX/UI, and future-feature simulation still pass after the implementation wave.
