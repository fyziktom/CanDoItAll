# Current State

- `CanDoItAll.Mcp.Components` and `CanDoItAll.Mcp.SshOps` both create an empty generic host, add stdio MCP server transport, build the host, and call `host.RunAsync()`.
- Neither entrypoint registers an application lifetime policy that stops the host after inactivity.
- `CanDoItAll.Mcp.Core.Hosting.McpHostBuilderExtensions` already owns shared MCP host conventions such as configuration, logging, and validated options.
- `ComponentsTools` has one private `ExecuteAsync` wrapper used by every Components MCP tool.
- `SshOpsTools` has one private async `ExecuteAsync` wrapper used by every SSH Ops MCP tool.
- The repo has focused Components MCP tests and broad unit tests that reference SshOps. There is no dedicated SshOps MCP tool test project.
- `Microsoft Testing Platform` is not used by the relevant test projects, so `mtp-hot-reload` is not part of this validation loop.
