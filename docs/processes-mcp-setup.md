# Processes MCP Transition

Last source review: 2026-07-28.

`CanDoItAll.Mcp.Processes` is not an active server. Do not install or call `candoitall_processes`.

Process launch, dispatch, operator actions, live projections, durable run records, graphs, and analytics are exposed by the web-hosted `/api/processes` control plane.

## Supported Replacement

1. Start the web host:

   ```powershell
   dotnet run --project src/App/CanDoItAll.Web/CanDoItAll.Web.csproj
   ```

2. Confirm `GET /api/access/status`.
3. Read `GET /api/processes/contract`.
4. Use OpenAPI for the exact query, request, and response schema.
5. Make the smallest focused call and read the run back through detail, summary, graph, history, or live projections.

Current references:

- [API control plane](api-control-plane.md)
- [Process operator runbook](process-agent-operator-runbook.md)
- [`ProcessesApi.cs`](../src/App/CanDoItAll.Web/Api/ProcessesApi.cs)
- [`ProcessRunRecordsApi.cs`](../src/App/CanDoItAll.Web/Api/ProcessRunRecordsApi.cs)
- [Canonical Processes API skill](https://github.com/fyziktom/CanDoItAll.SharedInfo/blob/main/codex/skills/candoitall-api-processes/SKILL.md)

## Local Configuration Cleanup

[`tools/Reinstall-CanDoItAllMcps.ps1`](../tools/Reinstall-CanDoItAllMcps.ps1) removes stale `mcp_servers.candoitall_processes` sections while reinstalling the supported development sidecars.

Old `Install-CanDoItAllProcessesMcp.ps1` instructions and `candoitall_processes` configuration are obsolete.
