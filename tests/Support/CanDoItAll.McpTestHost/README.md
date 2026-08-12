# CanDoItAll.McpTestHost

Provides a deterministic local stdio JSON-RPC peer for MCP transport, control-message,
and process-lifecycle integration tests.

This executable is test infrastructure only. It must not become a production dependency or application entry point.

```powershell
dotnet build .\tests\Support\CanDoItAll.McpTestHost\CanDoItAll.McpTestHost.csproj
```
