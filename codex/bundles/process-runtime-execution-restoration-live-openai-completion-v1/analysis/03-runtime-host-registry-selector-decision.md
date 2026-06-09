# Runtime Host / Registry / Selector / DI / Manager / Scheduler / Workflow Decision

## Needed now
These are needed now and must be proven:
- normal DI registration of process services and MAF integration services;
- process launch from UI, API, project-structure, scheduler and workflow-origin paths;
- scheduler-triggered process starts through `ProcessesService.StartRunFromTriggerAsync`;
- workflow-origin process starts through typed process services;
- MAF workflow-backed role and direct-agent execution under Processes;
- manager UI/read-only diagnostics and process manager directives through existing process services.

## Not needed yet
The following remain not approved:
- generic process-driver runtime host;
- driver registry;
- driver selector or fallback selector;
- driver DI auto-registration;
- driver manager command;
- scheduler/workflow hook into driver runtime;
- execution-capable domain driver.

## Why
A driver runtime should not become a second owner of process lifecycle. Processes already own lifecycle, claims, transitions, artifacts, finalizer, scheduler start, workflow start and MAF/direct-agent routes. Domain drivers currently provide read-only verification diagnostics over supplied facts.

## Future gate
A future execution-capable driver runtime may be proposed only after:
- runtime lifecycle ownership is specified;
- audit persistence exists;
- sandbox/allow-list policy exists;
- authorization and emergency stop exist;
- failure handoff and retry ownership are explicit;
- source-backed E2E process runtime proof is already green.
