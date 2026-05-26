# Current State

Phase7 moved the system in the right direction:

- Processes API has nested routes for runs, steps, artifacts, assignments, escalations, templates, and launch plans.
- API transition requests include `BlockCause`.
- API artifact requests include `ProjectionLineage`.
- Process read models expose more runtime governance data.
- The process skill `codex/skills/candoitall-api-processes/SKILL.md` exists.
- Blazor templates now include `AllowedOperations` and `OperationTargetScope`.

However, the next planned UI scenario, a Blazor WASM PWA Tetris app, will exercise the weakest remaining areas: template boundaries, strict operation enforcement, artifact proof, browser proof, project-structure writeback, and API skill quality.
