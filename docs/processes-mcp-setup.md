# Processes MCP Transition Note

## Current Status

`CanDoItAll.Mcp.Processes` is not an active MCP server in the current repository shape. The process MCP was suppressed after process definition, runtime, launch-plan, escalation, assignment, artifact, and analytics work moved behind the web-hosted HTTP API.

Do not reinstall or call `candoitall_processes` in current sessions. It may return later if there is a real capability gap, but current docs and skills should treat the HTTP API as the supported path.

## Current Replacement

Use the web API and the repo-managed process API skill:

- API overview: [API control plane](api-control-plane.md)
- Skill guidance: [codex/skills/candoitall-api-processes/SKILL.md](../codex/skills/candoitall-api-processes/SKILL.md)
- Source routes: [ProcessesApi.cs](../src/CanDoItAll.Web/Api/ProcessesApi.cs)
- Current implementation map: [Processes, MAF, and providers implementation map](processes-maf-providers-implementation-map.md)

Current source-grounded routes:

- `GET /api/processes/contract`
- `POST /api/processes/launch`
- `POST /api/processes/runs/{runId}/dispatch`
- `POST /api/processes/runs/{runId}/cancel`
- `POST /api/processes/runs/{runId}/steps/{stepInstanceId}/rework`
- `GET /api/processes/live`
- `GET /api/processes/runs/{runId}`
- `GET /api/processes/runs/{runId}/history`

The broader definition/template/artifact/assignment/escalation/approval/analytics route families described by older docs are not active in `ProcessesApi.cs`. Treat them as roadmap candidates, not current API.

## Migration Guidance

1. Start `src/CanDoItAll.Web`.
2. Check API status with `GET /api/access/status`.
3. If API authorization is enabled, use a Settings-generated bearer token or an already-authorized token.
4. Use the focused route for the smallest operation instead of fetching full process run detail by default.
5. Read back the run through `GET /api/processes/runs/{runId}`, `GET /api/processes/runs/{runId}/history`, or `GET /api/processes/live`.

## Removed Setup Commands

Old setup commands such as `Install-CanDoItAllProcessesMcp.ps1` and `candoitall_processes` should not be used for current work. The full MCP reinstall script now removes stale `candoitall_processes` config sections from local Codex config.
