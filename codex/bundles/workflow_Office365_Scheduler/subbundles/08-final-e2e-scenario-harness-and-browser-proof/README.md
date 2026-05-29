# 08-final-e2e-scenario-harness-and-browser-proof

## Objective

Close the bundle with real proof across Office365 fake Graph, workflow templates, project writes, and Scheduler Planner UX.

## Required Commands

- `dotnet restore CanDoItAll.slnx`
- `dotnet build CanDoItAll.slnx --no-restore`
- targeted unit tests:
  - Office365 executor tests
  - Workflow template loader tests
  - Scheduler input schema tests
  - Idempotency tests
- targeted integration tests:
  - fake Graph summary workflow
  - fake Graph task workflow
  - scheduler dispatch no-message
  - scheduler dispatch one-message
- component tests:
  - Workflows page template visibility
  - Scheduler typed form
- browser proof:
  - `/scheduler` desktop and narrow
  - target workflow selection
  - email/contact picker
  - project/node picker
  - every-two-hours quick schedule
  - `/agents/workflows` template visibility

## Closure Checklist

- New Office365 executor visible in plugin catalog and workflow toolbox.
- New templates visible in template pack and seed.
- Scheduler can configure the scenario without raw JSON.
- Raw JSON remains available for advanced use.
- No-message runs are not failures.
- Processed category mark happens after successful project write.
- Retry does not duplicate summary/tasks.
- No live Office365 credentials required in automated tests.
