# AgentFramework And Microsoft Agent Framework

This area owns provider-neutral agent execution contracts and adapters for Microsoft
Agent Framework, model providers, tools, skills, workflows, voice, Memory, and MCP
capabilities used by the application runtime.

| Area | Responsibility |
|---|---|
| [Common](Common/README.md) | Models, core orchestration, providers, persistence, hosting, voice, UI components, and MAF adapter |
| [Capabilities](Capabilities/README.md) | Capability identities, policy evaluation, and template compilation |
| [MCP](Mcp/README.md) | Application-runtime MCP contracts and transports |
| [Memory](Memory/README.md) | Memory integration for agent context, tools, and workflow execution |
| [Skills](Skills/README.md) | Skill contracts, descriptors, loading, and registration |
| [Tools](Tools/README.md) | Runtime tool contracts, descriptors, setup, and document tools |
| [Workflows](Workflows/README.md) | Workflow definitions, validation, runtime, templates, and MAF compilation |
| [Workflow executors](WorkflowExecutors/README.md) | Executor contracts, catalog, plugin bridge, and standard executors |

`MicrosoftAgentFramework.Packages.props` owns the Microsoft Agent Framework package
version set for this area. Provider-specific types must not leak into provider-neutral
domain contracts.
