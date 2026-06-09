# Scheduler And Workflow-Origin Process Starts

## Approved shape
Scheduler and workflow-origin starts call typed process services:
- `ProcessesService.StartRunFromTriggerAsync(ProcessRunTriggerStartRequest)`
- normal `StartRunAsync` lifecycle after validation

## Proof required
- scheduler target option points to process definition;
- scheduler fire creates process run with trigger source facts;
- workflow-origin test path creates process run with workflow source facts;
- both use process services and not driver runtime hooks;
- duplicate/invalid/missing source id paths fail predictably.

## Out of scope
- scheduler hook into process-driver runtime;
- workflow hook into process-driver runtime;
- driver-based process mutation.
