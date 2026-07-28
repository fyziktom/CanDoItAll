# Project Structure MCP Transition

Last source review: 2026-07-28.

`CanDoItAll.Mcp.ProjectStructure` is not an active server. Do not install or call `candoitall_projectstructure`.

Projects, hierarchy, tasks, nodes, assets, links, dependencies, leases, knowledge, analytics, and process/workflow node operations are exposed by the web-hosted `/api/project-structure` control plane.

## Supported Replacement

1. Start the web host:

   ```powershell
   dotnet run --project src/App/CanDoItAll.Web/CanDoItAll.Web.csproj
   ```

2. Confirm `GET /api/access/status`.
3. Inspect OpenAPI for the exact route and typed request.
4. Acquire the required project or repository/branch lease before shared mutations.
5. Make the smallest focused change and read back the affected structure, task, node, asset, link, or lease.

Current references:

- [API control plane](api-control-plane.md)
- [`ProjectStructureAgentApi.cs`](../src/App/CanDoItAll.Web/ProjectStructureAgentApi.cs)
- [Agent runtime tool surface](agent-runtime-tool-surface.md)
- [Canonical Project Structure API skill](https://github.com/fyziktom/CanDoItAll.SharedInfo/blob/main/codex/skills/candoitall-api-project-structure/SKILL.md)

The HTTP structure-read endpoint has no runtime invocation snapshot. `ContextDefault` resolves to canonical current state; explicit `InvocationSnapshot` is rejected rather than silently falling back.

## Local Configuration Cleanup

[`tools/Reinstall-CanDoItAllMcps.ps1`](../tools/Reinstall-CanDoItAllMcps.ps1) removes stale `mcp_servers.candoitall_projectstructure` sections while reinstalling the supported development sidecars.

Old `Install-CanDoItAllProjectStructureMcp.ps1` instructions and `candoitall_projectstructure` configuration are obsolete.
