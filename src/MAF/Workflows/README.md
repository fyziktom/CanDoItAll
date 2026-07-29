# Agent Workflows

Workflow projects define, validate, store, compile, and execute typed workflow graphs.

| Project | Responsibility |
|---|---|
| [Abstractions](CanDoItAll.AgentFramework.Workflows.Abstractions/README.md) | Workflow service, launch, query, and runtime contracts |
| [Builder](CanDoItAll.AgentFramework.Workflows.Builder/README.md) | Strongly typed workflow graph builders |
| [Core](CanDoItAll.AgentFramework.Workflows.Core/README.md) | Validation, catalog, analytics, and core services |
| [Runtime](CanDoItAll.AgentFramework.Workflows.Runtime/README.md) | Runs, checkpoints, artifacts, progress, and event delivery |
| [MAF adapter](CanDoItAll.AgentFramework.Workflows.MafAdapter/README.md) | Compilation and execution through Microsoft Agent Framework |
| [Templates](CanDoItAll.AgentFramework.Workflows.Templates/README.md) | Template loading, serialization, materialization, and validation |

Stored CanDoItAll workflow definitions are canonical. MAF is an execution adapter, not
the persisted domain model.
