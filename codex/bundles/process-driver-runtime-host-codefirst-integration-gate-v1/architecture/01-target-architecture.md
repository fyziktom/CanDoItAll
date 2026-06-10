# Target architecture

## Layer 1: Process Core
Pure deterministic rules and read models only. No drivers, EF, modules, UI, AgentFramework, OpenAI, storage, workspace, process runtime orchestration, or domain-specific execution concepts.

## Layer 2: Process Module Runtime
Owns definitions, templates, run start, outbox, dispatch, finalizer, artifacts, recovery, manager diagnostics, API/UI, scheduler/workflow-origin starts, and audit persistence.

## Layer 3: Verification Runtime Host
A process-module runtime service that handles read-only verification and dry-run planning. It may:

- select explicit lanes,
- apply options/limits/emergency disable,
- write durable audit,
- return structured success/denial/readback,
- evaluate dry-run execution plans.

It must not execute side effects.

## Layer 4: Domain Driver Packages
Verification-only packages over supplied evidence. They must not self-register, reflectively discover, execute, call external systems, write state, or mutate processes.

## Layer 5: Future Execution-Capable Host
Not approved. The current bundle may add contracts and dry-run plans that make future approval safer, but it must not execute effects.
