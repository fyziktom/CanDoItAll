# Assumptions And Risks

## Working Assumptions

- `ProjectObjectLinkKind.DependsOn` already represents the correct semantic direction: the selected node depends on the clicked prerequisite node.
- Adding explicit duration seconds should preserve existing start and end date fields rather than replace them.
- A workbench-scoped dependency analysis service can serve both UI or export and MCP readiness consumers without requiring a new storage subsystem.
- Existing Playwright and managed-SQLite infrastructure is sufficient for the required browser proof and richer seeded structures.

## Critical Path Risks

- If Phase 01 gets link direction, deletion semantics, or duration modeling wrong, every UI, export, and MCP consumer built afterward becomes misleading.
- If canvas runtime changes do not preserve drag behavior while dependency mode is active, the feature will feel broken even if persistence works.
- If dependency analysis and Gantt export use different graph interpretations, future MCP and Mermaid consumers will disagree about readiness and schedule order.

## Validation Risks

- Link hover and delete behavior may be difficult to prove through unit tests alone and must be backed by Playwright screenshots plus written findings.
- Duration defaults can look correct in code but still produce unstable Mermaid output if ordering and fallback dates are not deterministic.
- Fresh-SQLite proof can silently drift back to legacy data unless the test harness explicitly creates and switches to the new managed profile.

## Reopen Triggers

- Reopen Phase 01 if later phases discover that node summaries, migrations, or MCP contracts still lack the required duration or dependency metadata.
- Reopen Phase 02 if the toolbar modes cannot delete links cleanly or if the dependency-preview interaction forces regressions in drag and move behavior.
- Reopen Phase 03 if Mermaid export or readiness answers disagree with the persisted dependency graph in real project-structure data.
- Reopen Phase 04 if browser proof uses the wrong database profile, misses screenshots, or cannot visually demonstrate arrow direction and delete highlighting.
