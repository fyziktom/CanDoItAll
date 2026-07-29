# Source Projects

Production source is organized by architectural responsibility:

| Area | Responsibility |
|---|---|
| [App](App/README.md) | Executable web host and application composition |
| [Foundation](Foundation/README.md) | Shared primitives, infrastructure, migrations, and Git |
| [Integration](Integration/README.md) | Adapters for separately owned systems |
| [MAF](MAF/README.md) | AgentFramework and Microsoft Agent Framework integration |
| [Memory](Memory/README.md) | Provider-neutral Memory subsystem |
| [Modules](Modules/README.md) | Product-facing modules and Blazor surfaces |
| [Processes](Processes/README.md) | Durable process domain and runtime |
| [Plugins](plugins/README.md) | Plugin contracts and implementations |
| [UI](UI/README.md) | Application-owned reusable UI facades |

Dependency direction and communication rules are defined in the
[architecture overview](../docs/architecture/overview.md) and
[internal communication guide](../docs/architecture/internal-communication.md).
