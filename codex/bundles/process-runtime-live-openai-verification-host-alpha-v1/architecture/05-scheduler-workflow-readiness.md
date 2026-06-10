# Scheduler And Workflow Readiness

## Current state
Scheduler and workflow-origin starts use process services. This is correct and must remain.

## Next readiness target
Create a non-executing readiness map for future scheduled verification and workflow verification steps:
- what evidence would be read;
- what audit would be persisted;
- where the result would be visible;
- why it cannot mutate process state;
- which approval gate would be needed to enable it.

Do not add driver execution hooks in scheduler or workflow runtime in this bundle.
