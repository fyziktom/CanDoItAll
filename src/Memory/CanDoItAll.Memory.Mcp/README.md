# CanDoItAll.Memory.Mcp

Implements a Memory provider over the application MCP runtime, including configuration,
manifest creation, request mapping, invocation, response limits, and diagnostics.

The driver consumes MCP abstractions and Memory application contracts. It does not own
MCP process policy or Memory operation semantics.

```powershell
dotnet build .\src\Memory\CanDoItAll.Memory.Mcp\CanDoItAll.Memory.Mcp.csproj
```
