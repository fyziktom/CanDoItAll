# Processes MCP Setup

## Purpose

Use `CanDoItAll.Mcp.Processes` as a local stdio MCP for process-definition and process-run work inside this repository. It talks to the same CanDoItAll process module and database profiles that the web host uses; it is not a second remote API surface.

## What It Exposes

- process-definition listing, editor loading, save, publish, delete, import, and export
- process-run listing, detail lookup, start, step transition, and artifact recording
- supporting option lookups for parties and executor candidates
- process analytics and assignment-resolution helpers

## Standard Install

Run the full MCP reinstall from the repo root when you want the whole local MCP suite refreshed in one pass:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Reinstall-CanDoItAllMcps.ps1
```

That flow publishes the process MCP, syncs repo-managed skills, updates `.vscode\mcp.json`, updates `%USERPROFILE%\.codex\config.toml`, and records the entrypoint in `.artifacts\mcp-installs\install-manifest.json`.

## Focused Install

If you only need to republish the process MCP, run:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\Install-CanDoItAllProcessesMcp.ps1
```

## Settings

The local settings file is [CanDoItAll.Mcp.Processes.settings.json](/C:/repositories/CanDoItAll/CanDoItAll.Mcp.Processes.settings.json). It is intentionally small because this MCP is local-only:

```json
{
  "CanDoItAllMcpLaneKind": "PublishedActive",
  "Server": {
    "Name": "CanDoItAll.Mcp.Processes",
    "RepositoryRoot": ".",
    "EnsureCurrentProfileReadyOnStartup": true
  },
  "Processes": {
    "Runtime": {
      "RequirePostgreSqlForAgentAutomation": true
    }
  }
}
```

For governed runs that dispatch real AgentFramework agents, keep the active AppDbContext profile on PostgreSQL. The runtime guard intentionally blocks process-agent automation on SQLite when `RequirePostgreSqlForAgentAutomation` is enabled because SQLite becomes too slow for multi-step runs with tool receipts, artifacts, and recovery attempts.

## Restart Requirement

Codex and other MCP clients do not hot-discover new server registrations in the current session. After install or reinstall, restart the client so `candoitall_processes` is loaded and the new tool list becomes available.

## Safety Rules

- Keep process behavior canonical in `CanDoItAll.Modules.Processes`; do not fork definitions or runtime semantics inside the MCP.
- Treat this MCP as an orchestration surface, not a replacement for application services.
- Reinstall after significant process-tool changes so the published entrypoint and synced skill stay aligned with source.
