
# Master Codex prompt

You are implementing the CanDoItAll canvas-editor improvement bundle.

## Non-negotiable rules

1. Treat this bundle as the authoritative normalized specification for the uploaded notes.
2. Implement items in the dependency order defined in `04_PLAN/IMPLEMENTATION_SEQUENCE.md`.
3. Before each item, read:
   - the root architecture documents,
   - the item `README.md`,
   - `SPECIFICATION.md`,
   - `FILE_REFERENCES.md`,
   - `ACCEPTANCE_CRITERIA.md`,
   - `SCREENSHOT_REQUIREMENTS.md`.
4. Reuse existing modules and helpers before introducing new registries or helper layers.
5. Keep code comments in English.
6. Do not close a UI-changing item without screenshot evidence and a short semantic review of those screenshots.
7. Do not close the Prompt Factory 44-node bug item without root-cause evidence and regression coverage.

## Delivery protocol per item

For each item:

1. Inspect the referenced existing files.
2. Implement the feature according to the normalized decisions.
3. Add or update unit, component, integration, and Playwright tests where relevant.
4. Capture the required screenshots.
5. Write a concise evidence summary that references tests and screenshots.
6. Only then move to the next item.

## Anti-patterns to avoid

- Adding dozens of dedicated persistence columns instead of using structured metadata.
- Creating a full CRM for participant notes.
- Re-implementing repository, provider, or secret registries that already exist.
- Assuming a browser can launch a native terminal directly.
- Marking a task done because it “looks easy” without screenshot evidence.

## Final gate

The bundle is complete only when:
- all item checklists are satisfied,
- all required tests pass,
- all UI items have screenshot evidence,
- all original notes remain traceable through `05_TRACEABILITY/traceability_matrix.csv`.
