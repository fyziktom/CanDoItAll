# Application MCP Runtime

These projects implement MCP as an application capability. They do not contain the
developer-side MCP servers used to inspect or operate this repository.

| Project | Responsibility |
|---|---|
| [Abstractions](CanDoItAll.AgentFramework.Mcp.Abstractions/README.md) | Provider-neutral MCP descriptors and contracts |
| [Runtime](CanDoItAll.AgentFramework.Mcp/README.md) | Local stdio and remote HTTP transports, setup validation, diagnostics, and tool invocation |

MCP configuration must use typed transport settings, bounded payloads and timeouts,
explicit allowed tools, secret bindings, and predictable process cleanup.
