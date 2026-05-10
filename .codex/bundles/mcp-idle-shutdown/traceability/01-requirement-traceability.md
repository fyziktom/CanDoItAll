# Requirement Traceability

| Requirement | Inputs | Source refs | Owning subbundle | Planned proof | Closure |
| --- | --- | --- | --- | --- | --- |
| R001 | N001, N003 | `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\Hosting\McpHostBuilderExtensions.cs`; `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\Hosting\McpIdleShutdown.cs` | `01-shared-idle-shutdown` | `dotnet test tests\CanDoItAll.Mcp.Components.Tests\CanDoItAll.Mcp.Components.Tests.csproj --no-restore -m:1` | Solved |
| R002 | N001, N003 | `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Core\Hosting\McpIdleShutdown.cs` | `01-shared-idle-shutdown` | `McpIdleShutdownTests.Evaluate_Does_Not_Stop_While_Operation_Is_Active` in the Components MCP test run | Solved |
| R003 | N002, N003 | `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Configuration\McpServerOptions.cs`; `C:\repositories\CanDoItAll\CanDoItAll.Mcp.Components.settings.json` | `01-shared-idle-shutdown` | Components MCP tests and source review | Solved |
| R004 | N003 | `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.SshOps\Configuration\McpServerOptions.cs`; `C:\repositories\CanDoItAll\CanDoItAll.Mcp.SshOps.settings.json` | `01-shared-idle-shutdown` | `dotnet test tests\CanDoItAll.Tests.Unit\CanDoItAll.Tests.Unit.csproj --no-restore -m:1 --filter FullyQualifiedName~SshOpsIdleShutdownOptionsTests` | Solved |
| R005 | N001, N003 | `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.Components\Tools\ComponentsTools.cs`; `C:\repositories\CanDoItAll\src\CanDoItAll.Mcp.SshOps\Tools\SshOpsTools.cs` | `01-shared-idle-shutdown` | Components MCP tests plus MCP project builds | Solved |
