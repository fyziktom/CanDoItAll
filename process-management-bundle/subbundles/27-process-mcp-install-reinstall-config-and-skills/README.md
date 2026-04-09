# 27 Process MCP Install Reinstall Config And Skills

## Status

- `Completed`

## Objective

- Make the new process MCP installable, discoverable, and restart-ready by wiring repo scripts, settings, Codex config, VS Code config, manifest output, and repo-managed skill sync.

## Covered Inputs

- `REQ-025`
- `REQ-026`
- User request to update reinstall script, skills, install it, and prepare for restart-driven validation

## Prerequisites

- `26-process-local-mcp-server-and-tool-contracts`

## Exact Source References

- `C:\repositories\CanDoItAll\process-management-bundle\inputs\02-structured-input.md`
- `C:\repositories\CanDoItAll\process-management-bundle\analysis\01-current-state.md`
- `C:\repositories\CanDoItAll\tools\Install-CanDoItAllProjectStructureMcp.ps1`
- `C:\repositories\CanDoItAll\tools\Reinstall-CanDoItAllMcps.ps1`
- `C:\repositories\CanDoItAll\codex\scripts\install-candoitall-skills.ps1`
- `C:\repositories\CanDoItAll\docs\project-structure-mcp-setup.md`
- `C:\repositories\CanDoItAll\.vscode\mcp.json`

## Deliverables

- A process-MCP settings file and install script.
- Reinstall-script support that publishes the process MCP and updates VS Code and Codex MCP config.
- Install-manifest coverage for the process MCP.
- A repo-managed skill or skill update that documents how to use the new process MCP after install.
- Installed local artifacts so the MCP becomes available after restart.

## Dependency Impact

- This closes the gap between `server exists` and `the tool is actually usable by agents and local editors`.
- If this work is partial, the MCP will become another hidden local dependency that only works on the machine that built it manually.

## Validation Depth

- `Critical installation and discoverability proof`

## Implementation Steps

1. Add an installer/settings pattern for the process MCP consistent with the repo’s existing MCP setup.
2. Extend the reinstall script to publish the new MCP, update `.vscode\mcp.json`, update `~/.codex/config.toml`, and record the new install in the manifest.
3. Add or update repo-managed skill content so future sessions know the process MCP exists and how it should be used.
4. Run the reinstall/install flow locally and verify the generated settings and config files reference the new process MCP.
5. Record the restart requirement explicitly because the current session will not gain the new MCP tool list dynamically.

## Scope Exceptions

- Do not claim in-session tool availability after install if a Codex restart is still required.

## Do Not Do

- Do not leave the process MCP out of the central reinstall script.
- Do not rely on undocumented manual edits to `.vscode\mcp.json` or `config.toml`.
- Do not add a skill entry without actually syncing the repo-managed skill folder through the existing install flow.

## Acceptance Checklist

- The repo contains process-MCP install/config wiring.
- `tools\Reinstall-CanDoItAllMcps.ps1` publishes and records the process MCP.
- `.vscode\mcp.json` and `~/.codex/config.toml` are updated with the process MCP entry.
- Repo-managed skill content is updated and synced locally.
- Local install proof is captured and the restart requirement is explicit.

## Proof Required

- Install or reinstall command output showing the process MCP publish/install path.
- Generated or updated config-file proof for `.vscode\mcp.json`, `config.toml`, settings JSON, and install manifest.
- Skill-sync proof showing the new or updated process-MCP skill landed under `~/.codex\skills`.

## Browser Validation Logging

- `N/A`

## Progression Gate

- Phase07 may not close until the process MCP is both implemented and installed in the repo’s standard MCP workflow with explicit restart guidance.

## Suggested Agent Prompt

```text
Implement only the process-MCP install and discoverability slice. Update install/reinstall/config/skill wiring, run the reinstall flow, verify the generated config and manifest outputs, sync the repo-managed skill, and record the required Codex restart honestly.
```
