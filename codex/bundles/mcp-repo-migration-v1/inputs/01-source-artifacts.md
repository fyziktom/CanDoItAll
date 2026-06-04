# Source Artifacts

| ID | Type | Location | Notes |
| --- | --- | --- | --- |
| `SRC001` | Main repository | `repo://` | Current CanDoItAll application repository and source of Codex skills/settings. |
| `SRC002` | MCP repository | `C:\repositories\CanDoItAll.Mcp` | Destination repository created by the user. |
| `SRC003` | Reinstall script | `repo://tools/Reinstall-CanDoItAllMcps.ps1` | Must keep skill sync from the main repo but build MCP projects from the MCP repo. |
| `SRC004` | Current solution | `repo://CanDoItAll.slnx` | Contains MCP source, MCP tests, and tray project entries that must be removed. |
| `SRC005` | Artifact root | `repo://.artifacts` | Contains historical MCP installs and shadow builds that must be cleaned. |
| `SRC006` | MCP settings | `repo://CanDoItAll.Mcp.*.settings.json` | Workspace-specific settings stay in the main repo. |
| `SRC007` | Repo skills | `repo://codex/skills` | Skill source remains in the main repo and is synced by resetup. |
