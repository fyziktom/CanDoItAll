# Workflow Executors

Workflow executors map a typed workflow node to bounded application behavior.

| Area | Responsibility |
|---|---|
| [Abstractions](CanDoItAll.AgentFramework.WorkflowExecutors.Abstractions/README.md) | Executor contracts, contributions, and observability |
| [Core](CanDoItAll.AgentFramework.WorkflowExecutors.Core/README.md) | Catalog, policy, invocation, diagnostics, and registration |
| [Plugins](CanDoItAll.AgentFramework.WorkflowExecutors.Plugins/README.md) | Plugin-backed executor activation and grants |
| [Standard](Standard/README.md) | Built-in control, document, media, network, project, transform, and workspace executors |

Executors return typed outputs and diagnostics. They do not bypass capability, workspace,
secret, OAuth, or approval checks owned by the invoked boundary.
