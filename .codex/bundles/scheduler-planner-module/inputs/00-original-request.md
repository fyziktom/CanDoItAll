# Original Request

Use `candoitall-bundle-workflow` to prepare bundle to solve this:

Main goal:
adding module with scheduler/planner.
Main purpose is to plan automatic runs of workflows or processes.

Architect notes:
we already have Quartz that should do the triggering based on setup. Assure that it uses Db for possible recovery.
we must use CRON description for scheduling info for workflow/process (runs).
It must contains own page splitted to tabs with possibility to see actual scheduled runs, setup new schedule of run, Search history of old runs.
Use `imagegen` with `gpt-image-2` to create proposals of UI layouts.

first do proper analysis and prepare bundle only. Do not do implementations now.
