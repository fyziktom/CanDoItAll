# Implementation Prompt

Implement only the assigned subbundle from `codex/bundles/api-docs-skills-parity-v1`.

Before editing, read the subbundle README, `README.md`, `plan/01-phase-plan.md`, `requirements/01-normalized-requirements.md`, and the relevant workbook sheets in `inventories/api-docs-skills-gap-map.xlsx`.

Use source files as the contract authority. Prefer the smallest correct change. Keep API/docs/skills/tool repairs strongly typed and explicit; do not add silent fallback behavior that hides missing route or tool coverage.

For every subbundle:

- Confirm prerequisites and stop if an upstream gate is stale.
- Record changed files, commands, and proof in `reviews/01-execution-report.md`.
- Regenerate the workbook if source routes, DTO maps, docs/skills status, or gap decisions change.
- Run the subbundle-specific validation commands.
- If UI changes are introduced, add browser validation analytics and screenshots before closure.

Stop conditions:

- Route counts differ from the workbook and SB01 has not been reopened.
- A skill claim cannot be tied to source, test proof, or an explicit HTTP-only exception.
- Active skill sync cannot be completed after repo skill edits.
- Integration tests are blocked and no concrete blocker/proof alternative is recorded.
