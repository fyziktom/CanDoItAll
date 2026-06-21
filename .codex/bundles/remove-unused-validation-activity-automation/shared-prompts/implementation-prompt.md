# Implementation Prompt

Implement the active subbundle only. Before editing, reread the subbundle README, `requirements/01-normalized-requirements.md`, and `inventories/01-scope-inventory.md`.

Respect these boundaries:

- Remove direct dependencies on `CanDoItAll.Modules.Validation`, `CanDoItAll.Modules.Activity`, and `CanDoItAll.Modules.Automation`.
- Do not remove unrelated domain validation, workflow automation, scheduler, process, or project-structure behavior just because the words match.
- Keep SchedulerPlanner functional without importing the old Automation module.
- Capture command proof under `proof/SBxx/transcripts/` and update `reviews/01-execution-report.md` while evidence is fresh.
