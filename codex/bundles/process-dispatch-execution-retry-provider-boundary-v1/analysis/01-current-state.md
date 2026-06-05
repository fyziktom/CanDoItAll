# Current-State Analysis

## What is now stable enough

The previous sequence has already established several useful module-local boundaries:

- Agent runtime tool provider decoupling from MAF.
- Process automation execution client/snapshot boundary.
- Artifact projection planners/adapters/write coordinators.
- Artifact validation rule helpers.
- Tool validation/recovery helpers.
- Step completion finalizer helper partials.
- Dispatch route/candidate hydration/candidate factory/cooperation boundaries.
- Pre-execution guard/materialization boundaries.
- Subprocess runtime/projection boundaries.
- Implementation proof/runtime evidence helpers.
- Artifact satisfaction/evidence and residual validation helpers.

These are good preparation steps, but the process runtime is still not ready for a full Process Core split.

## Why Process Core is still deferred

`Execution.cs` and `Concurrency.cs` still mix too many responsibilities and are still tightly bound to:

- process dispatch candidates,
- execution-client calls,
- attempt loops,
- provider fallback side effects,
- recovery journal writes,
- process-specific retry rules,
- no-progress ledger compression,
- implementation proof and artifact validation helpers.

A Process Core split before these local seams are stable would likely create either a giant `Processes.Core` dependency magnet or public contracts that are still shaped around current dispatcher internals.

## Next safe seam

The next seam should isolate execution-attempt, retry, no-progress, and provider-recovery behavior into module-local helpers/coordinators. This prepares for a later Process Core and for future process helper drivers, without introducing either yet.
