# Scope Inventory

| Area | In Scope | Out Of Scope |
| --- | --- | --- |
| MCP projects | `CanDoItAll.Mcp.ProjectStructure`, `CanDoItAll.Mcp.Processes` | CodeAnalytics, Components, DotNetWatch, Mermaid, SshOps, LocalRuntime, MCP Core |
| Tests | Dedicated MCP tests and integration tests that launch those MCPs | Project/workbench/process API/domain tests that do not depend on removed MCP assemblies |
| Scripts | Reinstall script, two dedicated install scripts, generated MCP configs | Web app installer, Tailwind watcher |
| UI | Project Structure MCP Settings tab/panel | API Access JWT tab, workspace/provider/secret/storage settings |
| Skills | Replace MCP-use skills with API-use skills | Bundle workflow skills and unrelated public skills |
