# QA Prompt

Validate the Scheduler/Planner bundle implementation against `requirements/01-normalized-requirements.md` and `traceability/01-requirement-traceability.md`.

Prioritize:

- Quartz persistent-store/recovery proof, not only `Automation_Triggers` rehydration.
- Durable fire handling, dedupe, and explicit failed states.
- Typed process/workflow target adapters and run correlation.
- CRON description correctness for Quartz-style expressions.
- UI correctness across `Scheduled runs`, `New schedule`, and `Run history` tabs.
- Existing Automation runtime regressions.

Record test commands, browser screenshots, failures, fixes, and residual risk in `reviews/01-execution-report.md`.
