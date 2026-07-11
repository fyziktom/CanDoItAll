# Hand off repaired implementation slice

Produce a parent-ready handoff for a repaired and accepted implementation slice.

Include:

- Chosen slice behavior and exclusions.
- Initial child implementation summary.
- Slice validation failure that triggered repair.
- Repair child run summary.
- Recheck commands, exit codes, browser/runtime proof when applicable, and evidence refs.
- Residual risks and explicit parent recommendation.
- A criterion coverage table copied from the authoritative `slice-scope-packet`, with the owning production surface and current proof for every core behavior.

Do not present unresolved repaired proof as accepted. If recheck selected `slice-repair-escalation`, this step must remain skipped.
