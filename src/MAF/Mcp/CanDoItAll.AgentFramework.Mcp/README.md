# CanDoItAll.AgentFramework.Mcp

Implements local stdio and remote HTTP MCP clients, process lifecycle, JSON-RPC framing,
payload limits, setup validation, diagnostics, and tool result parsing.

Commands, endpoints, secrets, allowed tools, timeouts, and payload sizes are validated
before use. This project adapts MCP to provider-neutral capability contracts.

```powershell
dotnet build .\src\MAF\Mcp\CanDoItAll.AgentFramework.Mcp\CanDoItAll.AgentFramework.Mcp.csproj
```
