# candoitall-processes-mcp

Use when working with the local `CanDoItAll.Mcp.Processes` server for process definitions, runtime instances, assignments, artifacts, and analytics inside the CanDoItAll repository.

## Use This Skill When

- You need to inspect or edit process definitions through MCP instead of touching database tables directly.
- You need to inspect or advance process runs from a tool-driven workflow.
- You need to confirm whether the local process MCP is installed and restart-ready.

## Hard Rules

- Keep `CanDoItAll.Modules.Processes` as the source of truth. The MCP is an access surface, not a duplicate domain model.
- If the `candoitall_processes` server or its tools are missing, first reinstall it with `tools\Reinstall-CanDoItAllMcps.ps1` or `tools\Install-CanDoItAllProcessesMcp.ps1`, then restart Codex before claiming the MCP is available.
- Prefer MCP tools over raw database edits for process-definition or process-run operations.

## Tool Families

- Definitions: list, editor-get, save, publish, delete, import, export
- Runtime: run-list, run-detail, start, step-transition
- Support: analytics, assignment resolution, artifact recording, party options, executor options

## Validation Workflow

1. Confirm [CanDoItAll.Mcp.Processes.settings.json](/C:/repositories/CanDoItAll/CanDoItAll.Mcp.Processes.settings.json) exists and points at the repo root.
2. Confirm `.vscode\mcp.json` and `%USERPROFILE%\.codex\config.toml` contain `candoitall_processes`.
3. If the current session still lacks the tool list, restart the client before continuing.

## Related Docs

- [Processes MCP Setup](/C:/repositories/CanDoItAll/docs/processes-mcp-setup.md)
