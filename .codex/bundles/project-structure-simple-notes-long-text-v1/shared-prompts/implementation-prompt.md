# Implementation Prompt

Implement the selected subbundle only.

Before editing, reread `bundle://inputs/00-original-request.md`, `bundle://requirements/01-normalized-requirements.md`, `bundle://plan/01-phase-plan.md`, and the selected subbundle README. Confirm the entry gate in `bundle://reviews/01-execution-report.md`.

Use the smallest correct change set:

- Keep full simple note bodies in `Notes`.
- Derive note titles from the first non-empty line when quick-note create/edit supplies only a body.
- Keep CanvasLib sizing/rendering changes inside CanvasLib runtime assets or the local package artifact; do not add page-local wrappers.
- If updating CanvasLib package assets, rebuild/update the consumed package and prove the app/test run uses the updated package.

Stop and repair the bundle if implementation reality shows a missing source path, a wider scope than simple notes, or a proof method that cannot detect losing the long note body.

After implementation, update `reviews/01-execution-report.md`, proof manifests, semantic invariants, and raw-note closure rows while evidence is fresh.
