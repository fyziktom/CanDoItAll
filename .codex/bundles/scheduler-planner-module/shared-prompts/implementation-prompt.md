# Implementation Prompt

Implement the active subbundle only from `C:\repositories\CanDoItAll\.codex\bundles\scheduler-planner-module`.

Rules:

- Do not broaden the scope beyond the subbundle README.
- Preserve the architecture boundary: SchedulerPlanner owns workflow/process scheduling; Automation remains generic Quartz/durable-message infrastructure.
- Use strongly typed enums/value objects/contracts instead of magic strings for schedule targets, statuses, owner keys, and target adapters.
- Do not add silent fallbacks for invalid CRON, unsupported database providers, missing targets, or failed launches.
- Use existing BaseLib/Radzen-style wrappers for UI work.
- Update `reviews/01-execution-report.md` with commands, evidence paths, decisions, and residual risks before closing the subbundle.
- Stop and report if the progression gate cannot honestly pass.
