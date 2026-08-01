# Processes

The Processes area owns durable process definitions, compiled plans, drivers, execution,
recovery, projections, persistence, and template materialization.

| Project | Responsibility |
|---|---|
| [Contracts](CanDoItAll.Processes.Contracts/README.md) | Versioned public process payloads and operation contracts |
| [Abstractions](CanDoItAll.Processes.Abstractions/README.md) | Strongly typed process identities |
| [Core](CanDoItAll.Processes.Core/README.md) | Graph rules, state transitions, events, branches, and artifacts |
| [Builder](CanDoItAll.Processes.Builder/README.md) | Definition compilation and deterministic plan hashing |
| [Application](CanDoItAll.Processes.Application/README.md) | Launch, orchestration, recovery, assignment, and audit services |
| [Runtime](CanDoItAll.Processes.Runtime/README.md) | Process manager, step execution, recovery, and runtime control |
| [Projections](CanDoItAll.Processes.Projections/README.md) | Read models, projection workers, and query contracts |
| [Persistence](CanDoItAll.Processes.Persistence/README.md) | EF Core event, run, plan, projection, outbox, and assignment stores |
| [Templates](CanDoItAll.Processes.Templates/README.md) | Process template loading, compatibility, and materialization |
| [Drivers](Drivers/README.md) | Execution adapter contracts and standard strategies |

Process state changes flow through the runtime and persisted event/record contracts.
Pages, agents, workflows, and adapters must not write process state directly.
