# Implementation Prompt

Implement only the active subbundle from `material-icons-migration-bundle-v1`.

Before editing:

- Read `README.md`, `plan/01-phase-plan.md`, `traceability/01-requirement-traceability.md`, and the active subbundle README.
- Open the relevant rows in `C:/repositories/CanDoItAll/output/spreadsheet/material-icons-migration-tracker.xlsx` and the CSV exports so you know which files and tokens are in scope.
- Confirm the prerequisite subbundles are complete and still trusted.

Execution rules:

- Do not reintroduce a remote icon stylesheet or font request.
- Prefer the shared BaseLib icon foundation over one-off route-level icon rendering.
- Replace raw glyph spans and text icons with Material icon rendering or explicitly mapped Material-compatible tokens.
- Preserve or improve icon-only accessibility labels.
- Respect the existing dirty worktree and merge around local edits instead of overwriting them.
- Update the workbook status, proposed Material icon, and validation notes as you complete rows.

Proof rules:

- Run the required build or test commands listed in the active subbundle.
- Capture the browser evidence listed in the active subbundle and write it into `reviews/01-execution-report.md` while the evidence is fresh.
- If the proof is weak, incomplete, or blocked, do not close the subbundle; record the blocker and stop.
