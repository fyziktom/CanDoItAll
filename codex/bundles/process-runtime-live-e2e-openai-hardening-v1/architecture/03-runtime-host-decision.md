# Runtime Host / Registry / Selector Decision

## Current decision

Do not add a generic process-driver runtime host, driver registry, runtime selector, driver DI auto-registration, driver manager command, scheduler driver hook, or workflow driver hook in this bundle.

## Why

The immediate goal is to restore normal process execution. That uses existing process runtime ownership:

- `ProcessesService` starts runs.
- `ProcessRunAutomationDispatchService` dispatches and finalizes steps.
- MAF/workflow/direct-agent execution remains underneath process ownership.
- Scheduler/workflow-origin starts call typed process services.
- Read-only driver verification is only diagnostics.

Adding a generic driver host now would create a second runtime owner before the first runtime is fully proven live.

## What is needed now instead

- Stronger UI/API/project-structure/scheduler/workflow process start tests.
- Real app startup proof.
- Real OpenAI smoke tests under budget/secret controls.
- Worker/outbox/claim/finalizer proofs.
- Manager-visible diagnostics and recovery proof.

## Future approval gate

Runtime host work can start only if a future bundle proves:
- stable process runtime release-candidate,
- lifecycle owner,
- security model,
- audit persistence,
- sandbox/allow-list model,
- authorization/approval workflow,
- observability,
- rollback/emergency stop,
- exact DI/registration strategy,
- red-team negative tests.
