You are a senior C#/.NET architect working in `C:\repositories\CanDoItAll` on branch `processes-hardening`.

Implement the bundle at `codex/bundles/workflow-office365-scheduler-followup` one subbundle at a time. Start with SB01 baseline proof and stop if the baseline fails for reasons unrelated to your changes. Follow the subbundle order. Keep all source-code comments in English. Capture restore/build/test evidence under each `proof/SBxx/` folder and update `reviews/01-execution-report.md`.

Primary goal: add an Office365 workflow executor that downloads one unprocessed email matching a configured email address, add summary/task workflow templates that mark the email as processed, and harden Scheduler Planner so a user can schedule these workflows every N hours using a friendly form with contact/email, project, and parent node selection rather than raw JSON only.
